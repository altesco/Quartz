using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using Quartz.UI.CodeCompletion;
using Quartz.UI.Services;
using Quartz.UI.Tools;
using TextMateSharp.Grammars;

namespace Quartz.UI.Behaviors
{
    public class TextEditorBehavior : Behavior<TextEditor>
    {
        private RegistryOptions? _registryOptions;
        private TextMate.Installation? _textMateInstallation;
        private YamlIndentationRenderer? _indentationRenderer;
        private TextDocument? _trackedDocument;
        private string? _currentScope;
        private ViewModels.EditorVM? _subscribedVm;
        private QuartzCompletionWindow? _completionWindow;
        private readonly DispatcherTimer _completionTimer;
        private const int CompletionDelayMs = 400;

        private static readonly bool IsWayland =
            OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        private Key? _currentPressedKey;
        private bool _hasReceivedTextInputForCurrentKey;
        private bool _triggerOnNewLine;

        public TextEditorBehavior()
        {
            _completionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(CompletionDelayMs)
            };
            _completionTimer.Tick += OnCompletionTimerTick;
        }

        private void OnCompletionTimerTick(object? sender, EventArgs e)
        {
            _completionTimer.Stop();
            if (_completionWindow == null && AssociatedObject?.TextArea?.IsFocused == true)
            {
                ShowCompletion(forced: false);
            }
            else
            {
                _triggerOnNewLine = false;
            }
        }

        private void HookVm(ViewModels.EditorVM? newVm)
        {
            if (_subscribedVm == newVm) return;

            if (_subscribedVm != null)
            {
                _subscribedVm.NavigationRequested -= OnNavigationRequested;
                _subscribedVm.PropertyChanged -= OnSubscribedVmPropertyChanged;
            }

            _subscribedVm = newVm;

            if (_subscribedVm != null)
            {
                _subscribedVm.NavigationRequested += OnNavigationRequested;
                _subscribedVm.PropertyChanged += OnSubscribedVmPropertyChanged;
                if (AssociatedObject != null)
                {
                    AssociatedObject.Options.IndentationSize = _subscribedVm.IndentationSize;
                }
                UpdateCaretPosition();
                var pending = _subscribedVm.ConsumePendingNavigation();
                if (pending.HasValue)
                {
                    NavigateTo(pending.Value.Line, pending.Value.Column);
                }
            }
        }

        private void OnSubscribedVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.EditorVM.IndentationSize) && AssociatedObject != null && _subscribedVm != null)
            {
                AssociatedObject.Options.IndentationSize = _subscribedVm.IndentationSize;
            }
        }

        private void OnCaretPositionChanged(object? sender, EventArgs e)
        {
            UpdateCaretPosition();
        }

        private void UpdateCaretPosition()
        {
            if (_subscribedVm != null && AssociatedObject?.TextArea?.Caret != null)
            {
                _subscribedVm.CaretLine = AssociatedObject.TextArea.Caret.Line;
                _subscribedVm.CaretColumn = AssociatedObject.TextArea.Caret.Column;
            }
        }

        private void OnNavigationRequested(long line, long col)
        {
            _subscribedVm?.ConsumePendingNavigation();
            NavigateTo(line, col);
        }

        public const int MaxLineLengthForHighlighting = 500;
        private const double MinFontSize = 6;
        private const double MaxFontSize = 72;
        private const double ZoomStep = 1.0;

        private readonly PinchGestureRecognizer _pinchRecognizer = new();
        private double _pinchStartFontSize;
        private bool _isPinchActive;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is null) return;

            // Подписки
            AssociatedObject.Loaded += OnEditorLoaded;
            AssociatedObject.DataContextChanged += OnDataContextChanged;
            AssociatedObject.ActualThemeVariantChanged += OnActualThemeVariantChanged;
            AssociatedObject.PropertyChanged += OnPropertyChanged;

            if (Avalonia.Application.Current != null)
            {
                Avalonia.Application.Current.ActualThemeVariantChanged += OnGlobalThemeChanged;
            }

            HookDocument(AssociatedObject.Document);
            UpdateThemeColors();
            ConfigureEditorForCurrentFile();
            HookVm(AssociatedObject.DataContext as ViewModels.EditorVM);

            if (AssociatedObject.TextArea != null)
            {
                AssociatedObject.TextArea.IndentationStrategy = new YamlIndentationStrategy(AssociatedObject);
                if (AssociatedObject.TextArea.Caret != null)
                {
                    AssociatedObject.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
                }
                AssociatedObject.TextArea.TextEntered += OnTextAreaTextEntered;
                AssociatedObject.TextArea.LostFocus += OnTextAreaLostFocus;
                AssociatedObject.TextArea.AddHandler(
                    InputElement.PointerPressedEvent,
                    OnTextAreaPointerPressed,
                    RoutingStrategies.Tunnel);
            }
            UpdateCaretPosition();

            AssociatedObject.GestureRecognizers.Add(_pinchRecognizer);

            AssociatedObject.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel);

            AssociatedObject.AddHandler(
                InputElement.PinchEvent,
                OnPinchGesture,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            AssociatedObject.AddHandler(
                InputElement.PinchEndedEvent,
                OnPinchGestureEnded,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            AssociatedObject.AddHandler(
                InputElement.PointerTouchPadGestureMagnifyEvent,
                OnTouchPadGestureMagnify,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            AssociatedObject.AddHandler(
                InputElement.KeyDownEvent,
                OnEditorKeyDown,
                RoutingStrategies.Tunnel);

            AssociatedObject.AddHandler(
                InputElement.KeyUpEvent,
                OnEditorKeyUp,
                RoutingStrategies.Tunnel);

            LinuxX11PinchGestureService.PinchGesture -= OnLinuxPinchGesture;
            LinuxX11PinchGestureService.PinchGesture += OnLinuxPinchGesture;
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextEditor.DocumentProperty)
            {
                HookDocument(e.GetNewValue<TextDocument?>());
                ConfigureEditorForCurrentFile();
                if (_subscribedVm != null)
                {
                    var pending = _subscribedVm.ConsumePendingNavigation();
                    if (pending.HasValue)
                    {
                        NavigateTo(pending.Value.Line, pending.Value.Column);
                    }
                }
            }
        }

        private void HookDocument(TextDocument? newDoc)
        {
            if (_trackedDocument != null)
            {
                _trackedDocument.TextChanged -= OnDocumentTextChanged;
            }

            _trackedDocument = newDoc;

            if (_trackedDocument != null)
            {
                _trackedDocument.TextChanged += OnDocumentTextChanged;
            }
        }

        private void OnDocumentTextChanged(object? sender, EventArgs e) => ConfigureEditorForCurrentFile();

        private void OnEditorLoaded(object? sender, RoutedEventArgs e)
        {
            UpdateThemeColors();
            ConfigureEditorForCurrentFile();
            HookVm(AssociatedObject?.DataContext as ViewModels.EditorVM);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            ConfigureEditorForCurrentFile();
            HookVm(AssociatedObject?.DataContext as ViewModels.EditorVM);
        }

        private void OnActualThemeVariantChanged(object? sender, EventArgs e) => UpdateThemeColors();

        private void OnGlobalThemeChanged(object? sender, EventArgs e) => UpdateThemeColors();

        private void EnsureTextMateInstalled()
        {
            if (AssociatedObject == null) return;
            if (_textMateInstallation != null) return;

            try
            {
                var targetTheme = Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;
                var isLight = targetTheme == ThemeVariant.Light;
                var initialTheme = isLight ? ThemeName.LightPlus : ThemeName.DarkPlus;

                _registryOptions ??= new RegistryOptions(initialTheme);
                _textMateInstallation = AssociatedObject.InstallTextMate(_registryOptions);

                var tmTheme = isLight ? ThemeName.LightPlus : ThemeName.DarkPlus;
                _textMateInstallation.SetTheme(_registryOptions.LoadTheme(tmTheme));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextMate init failed: {ex.Message}");
            }
        }

        private void RemoveTextMate()
        {
            if (_textMateInstallation != null)
            {
                try
                {
                    _textMateInstallation.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TextMate dispose failed: {ex.Message}");
                }
                _textMateInstallation = null;
            }
        }

        private void ConfigureEditorForCurrentFile()
        {
            if (AssociatedObject == null) return;

            if (_trackedDocument != AssociatedObject.Document)
            {
                HookDocument(AssociatedObject.Document);
            }

            var vm = AssociatedObject.DataContext as ViewModels.EditorVM;
            var filePath = vm?.FilePath;
            var ext = !string.IsNullOrEmpty(filePath) ? System.IO.Path.GetExtension(filePath).ToLowerInvariant() : "";
            bool isYaml = ext is ".pcby" or ".layy" or ".stly" or ".yaml" or ".yml";

            bool hasExcessiveLines = false;
            var doc = AssociatedObject.Document;
            if (doc != null)
            {
                if (doc.TextLength > 1_500_000)
                {
                    hasExcessiveLines = true;
                }
                else
                {
                    foreach (var line in doc.Lines)
                    {
                        if (line.Length > MaxLineLengthForHighlighting)
                        {
                            hasExcessiveLines = true;
                            break;
                        }
                    }
                }
            }

            // 1. Управление YamlIndentationRenderer (если нет сверхдлинных строк)
            if (!hasExcessiveLines)
            {
                if (_indentationRenderer == null && AssociatedObject.TextArea?.TextView != null)
                {
                    _indentationRenderer = new YamlIndentationRenderer(AssociatedObject);
                    AssociatedObject.TextArea.TextView.BackgroundRenderers.Add(_indentationRenderer);
                    UpdateIndentationLineColor();
                }
            }
            else
            {
                if (_indentationRenderer != null && AssociatedObject.TextArea?.TextView != null)
                {
                    AssociatedObject.TextArea.TextView.BackgroundRenderers.Remove(_indentationRenderer);
                    _indentationRenderer = null;
                }
            }

            // 2. Управление подсветкой TextMate
            if (hasExcessiveLines)
            {
                if (_textMateInstallation != null)
                {
                    RemoveTextMate();
                    _currentScope = null;
                    AssociatedObject.TextArea?.TextView?.Redraw();
                }
            }
            else
            {
                string? scopeName = null;
                if (isYaml)
                {
                    scopeName = _registryOptions?.GetScopeByExtension(".yaml");
                }
                else if (!string.IsNullOrEmpty(ext))
                {
                    scopeName = _registryOptions?.GetScopeByExtension(ext);
                }

                if (_textMateInstallation == null)
                {
                    EnsureTextMateInstalled();
                }

                if (_textMateInstallation != null && _currentScope != scopeName)
                {
                    _currentScope = scopeName;
                    if (!string.IsNullOrEmpty(scopeName))
                    {
                        try
                        {
                            _textMateInstallation.SetGrammar(scopeName);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"SetGrammar failed: {ex.Message}");
                        }
                    }
                    AssociatedObject.TextArea?.TextView?.Redraw();
                }
            }
        }

        private void UpdateThemeColors()
        {
            if (AssociatedObject?.TextArea?.TextView == null) return;

            var editor = AssociatedObject;
            var textArea = editor.TextArea;
            var textView = textArea.TextView;

            // 0. ОРИЕНТИРУЕМСЯ ТОЛЬКО НА ГЛОБАЛЬНУЮ ТЕМУ
            var targetTheme = Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;
            var isLight = targetTheme == ThemeVariant.Light;

            if (_textMateInstallation != null && _registryOptions != null)
            {
                var tmTheme = isLight ? ThemeName.LightPlus : ThemeName.DarkPlus;
                _textMateInstallation.SetTheme(_registryOptions.LoadTheme(tmTheme));
            }

            // 1. Вытягиваем цвета НАСИЛЬНО для нужной темы, обходя баги DataTemplate
            if (TryGetThemeColor("BackgroundColor", targetTheme, out var bgColor))
            {
                editor.Background = new SolidColorBrush(bgColor);
            }

            if (TryGetThemeColor("ForegroundColor", targetTheme, out var fgColor))
            {
                editor.Foreground = new SolidColorBrush(fgColor);
            }

            if (TryGetThemeColor("MutedColor", targetTheme, out var mutedColor))
            {
                editor.LineNumbersForeground = new SolidColorBrush(mutedColor);
            }

            // 2. Выделение текста
            textArea.SelectionCornerRadius = 4.0;
            textArea.SelectionBorder = null;

            if (TryGetThemeColor("SelectionColor", targetTheme, out var selColor))
            {
                textArea.SelectionBrush = new SolidColorBrush(selColor);
            }
            else if (TryGetThemeColor("PrimaryColor20", targetTheme, out var fallbackSelColor))
            {
                textArea.SelectionBrush = new SolidColorBrush(fallbackSelColor);
            }

            // 3. Подсветка строки
            if (TryGetThemeColor("GhostColor", targetTheme, out var ghostColor))
            {
                textView.CurrentLineBackground = new SolidColorBrush(ghostColor);
            }
            textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);

            // 4. Линии отступов
            UpdateIndentationLineColor();

            textView.Redraw();
        }

        private void UpdateIndentationLineColor()
        {
            if (_indentationRenderer == null) return;
            var targetTheme = Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;
            if (TryGetThemeColor("BorderColor60", targetTheme, out var borderColor))
            {
                _indentationRenderer.UpdateLineColor(borderColor);
            }
            else if (TryGetThemeColor("BorderColor", targetTheme, out var fallbackColor))
            {
                _indentationRenderer.UpdateLineColor(fallbackColor);
            }
        }

        // --- МОЙ СОБСТВЕННЫЙ ПОИСКОВИК РЕСУРСОВ ---
        private bool TryGetThemeColor(string key, ThemeVariant targetTheme, out Color color)
        {
            // Ручками идем вверх по визуальному дереву и заставляем каждый узел искать ресурс 
            // ИМЕННО ДЛЯ ТОЙ ТЕМЫ, которая нам нужна, а не для той, в которой он застрял!
            Visual? current = AssociatedObject;

            while (current != null)
            {
                if (current is IResourceNode node && node.TryGetResource(key, targetTheme, out var res))
                {
                    if (res is Color c) { color = c; return true; }
                    if (res is ISolidColorBrush b) { color = b.Color; return true; }
                }
                current = current.GetVisualParent();
            }

            // На крайний случай дергаем приложение напрямую
            if (Avalonia.Application.Current is IResourceNode appNode &&
                appNode.TryGetResource(key, targetTheme, out var appRes))
            {
                if (appRes is Color c) { color = c; return true; }
                if (appRes is ISolidColorBrush b) { color = b.Color; return true; }
            }

            color = default;
            return false;
        }

        private void OnPinchGesture(object? sender, PinchEventArgs e)
        {
            if (AssociatedObject == null || !AssociatedObject.IsEffectivelyVisible) return;

            if (!_isPinchActive)
            {
                _isPinchActive = true;
                _pinchStartFontSize = AssociatedObject.FontSize;
            }

            if (e.Scale <= 0.001) return;

            double targetFontSize = Math.Clamp(_pinchStartFontSize * e.Scale, MinFontSize, MaxFontSize);
            AssociatedObject.FontSize = Math.Round(targetFontSize, 2);
            e.Handled = true;
        }

        private void OnPinchGestureEnded(object? sender, PinchEndedEventArgs e)
        {
            _isPinchActive = false;
            e.Handled = true;
        }

        private void OnLinuxPinchGesture(object? sender, LinuxPinchEventArgs e)
        {
            if (e.Handled || AssociatedObject == null || !AssociatedObject.IsEffectivelyVisible) return;

            if (!_isPinchActive)
            {
                var localPoint = e.Window.TranslatePoint(e.WindowPosition, AssociatedObject);
                if (!localPoint.HasValue || !new Rect(AssociatedObject.Bounds.Size).Contains(localPoint.Value))
                    return;
            }

            if (e.State == LinuxGestureState.Begin)
            {
                _isPinchActive = true;
                _pinchStartFontSize = AssociatedObject.FontSize;
                e.Handled = true;
            }
            else if (e.State == LinuxGestureState.Update)
            {
                if (!_isPinchActive)
                {
                    _isPinchActive = true;
                    _pinchStartFontSize = AssociatedObject.FontSize;
                }

                if (e.Scale <= 0.001) return;

                double targetFontSize = Math.Clamp(_pinchStartFontSize * e.Scale, MinFontSize, MaxFontSize);
                AssociatedObject.FontSize = Math.Round(targetFontSize, 2);
                e.Handled = true;
            }
            else if (e.State == LinuxGestureState.End)
            {
                _isPinchActive = false;
                e.Handled = true;
            }
        }

        private void OnTouchPadGestureMagnify(object? sender, PointerDeltaEventArgs e)
        {
            if (AssociatedObject == null || !AssociatedObject.IsEffectivelyVisible) return;

            double delta = Math.Abs(e.Delta.Y) > 0.00001 ? e.Delta.Y : e.Delta.X;
            if (Math.Abs(delta) < 0.0001) return;

            double scaleFactor = Math.Exp(delta);
            double newFontSize = Math.Clamp(AssociatedObject.FontSize * scaleFactor, MinFontSize, MaxFontSize);
            AssociatedObject.FontSize = Math.Round(newFontSize, 2);
            e.Handled = true;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                _isPinchActive = false;
                e.Handled = true;

                if (Math.Abs(e.Delta.Y) < 0.001) return;

                double currentFontSize = AssociatedObject!.FontSize;
                double newFontSize;

                // Поддержка как дискретных щелчков колесика мыши (Delta.Y = +/-1.0),
                // так и плавного жеста сведения/разведения тачпада (дробные Delta.Y)
                if (Math.Abs(e.Delta.Y) >= 0.9)
                {
                    double direction = Math.Sign(e.Delta.Y);
                    newFontSize = currentFontSize + (direction * ZoomStep);
                }
                else
                {
                    double zoomMultiplier = Math.Pow(1.2, e.Delta.Y);
                    newFontSize = currentFontSize * zoomMultiplier;
                }

                newFontSize = Math.Clamp(Math.Round(newFontSize, 2), MinFontSize, MaxFontSize);

                if (Math.Abs(newFontSize - currentFontSize) > 0.001)
                {
                    AssociatedObject.FontSize = newFontSize;
                }
            }
        }

        private void OnEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (AssociatedObject == null) return;

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                _triggerOnNewLine = false;
                if (e.Key == Key.Space)
                {
                    _completionTimer.Stop();
                    e.Handled = true;
                    ShowCompletion(forced: true);
                    return;
                }
                else if (e.Key is Key.OemPlus or Key.Add)
                {
                    double newSize = Math.Clamp(AssociatedObject.FontSize + ZoomStep, MinFontSize, MaxFontSize);
                    AssociatedObject.FontSize = Math.Round(newSize, 2);
                    e.Handled = true;
                }
                else if (e.Key is Key.OemMinus or Key.Subtract)
                {
                    double newSize = Math.Clamp(AssociatedObject.FontSize - ZoomStep, MinFontSize, MaxFontSize);
                    AssociatedObject.FontSize = Math.Round(newSize, 2);
                    e.Handled = true;
                }
                else if (e.Key is Key.D0 or Key.NumPad0)
                {
                    AssociatedObject.FontSize = 14.0;
                    e.Handled = true;
                }
                else
                {
                    _completionTimer.Stop();
                    _completionWindow?.Close();
                }
            }
            else if (e.Key is Key.Escape)
            {
                _triggerOnNewLine = false;
                _completionTimer.Stop();
                _completionWindow?.Close();
            }
            else if (e.Key is Key.Enter)
            {
                _currentPressedKey = null;
                _hasReceivedTextInputForCurrentKey = false;
                if (_completionWindow == null)
                {
                    _triggerOnNewLine = true;
                    _completionTimer.Stop();
                    _completionTimer.Start();
                }
            }
            else if (e.Key is Key.Tab)
            {
                _triggerOnNewLine = false;
                _completionTimer.Stop();
            }
            else if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown)
            {
                _triggerOnNewLine = false;
                _completionTimer.Stop();
            }
            else if (e.Key is Key.Left or Key.Right or Key.Home or Key.End)
            {
                _triggerOnNewLine = false;
                _completionTimer.Stop();
                _completionWindow?.Close();
            }
            else if (e.Key is Key.Back or Key.Delete)
            {
                _triggerOnNewLine = false;
                _completionTimer.Stop();
                if (_completionWindow == null)
                {
                    _completionTimer.Start();
                }
            }
            else if (e.Key is not Key.LeftShift and not Key.RightShift and not Key.LeftCtrl and not Key.RightCtrl and not Key.LeftAlt and not Key.RightAlt and not Key.LWin and not Key.RWin and not Key.CapsLock and not Key.NumLock and not Key.Scroll)
            {
                _triggerOnNewLine = false;
                if (IsWayland &&
                    _currentPressedKey == e.Key &&
                    _hasReceivedTextInputForCurrentKey &&
                    !e.Handled &&
                    !string.IsNullOrEmpty(e.KeySymbol) &&
                    !char.IsControl(e.KeySymbol[0]) &&
                    !e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                    !e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
                    AssociatedObject.TextArea != null &&
                    !AssociatedObject.IsReadOnly)
                {
                    // На Wayland Avalonia.Wayland отправляет только RawKeyEventArgs на таймере повтора клавиш (OnKeyRepeatTick)
                    // и не генерирует RawTextInputEventArgs. Вставляем повторяемый символ напрямую.
                    AssociatedObject.TextArea.PerformTextInput(e.KeySymbol);
                    e.Handled = true;
                    return;
                }

                if (_currentPressedKey != e.Key)
                {
                    _currentPressedKey = e.Key;
                    _hasReceivedTextInputForCurrentKey = false;
                }

                // При непрерывном наборе или зажатии клавиши сбрасываем таймер задержки на каждом повторе ввода.
                // Окно автодополнения появится строго через 400мс после того, как клавишу отпустили.
                if (_completionWindow == null)
                {
                    _completionTimer.Stop();
                    _completionTimer.Start();
                }
            }
        }

        private void OnTextAreaPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _triggerOnNewLine = false;
            _currentPressedKey = null;
            _hasReceivedTextInputForCurrentKey = false;
            _completionTimer.Stop();
            _completionWindow?.Close();
        }

        private void OnEditorKeyUp(object? sender, KeyEventArgs e)
        {
            if (_currentPressedKey == e.Key)
            {
                _currentPressedKey = null;
                _hasReceivedTextInputForCurrentKey = false;
            }
        }

        private void OnTextAreaLostFocus(object? sender, RoutedEventArgs e)
        {
            _triggerOnNewLine = false;
            _currentPressedKey = null;
            _hasReceivedTextInputForCurrentKey = false;
        }

        private void OnTextAreaTextEntered(object? sender, TextInputEventArgs e)
        {
            if (_triggerOnNewLine)
            {
                if (string.IsNullOrWhiteSpace(e.Text))
                {
                    // Перенос строки и/или автоматические пробелы отступа.
                    // Не сбрасываем _triggerOnNewLine и не останавливаем таймер!
                    return;
                }
                _triggerOnNewLine = false;
            }

            _hasReceivedTextInputForCurrentKey = true;

            if (string.IsNullOrEmpty(e.Text)) return;
            if (AssociatedObject == null || AssociatedObject.IsReadOnly) return;

            // Если окно автодополнения уже открыто, не перезапускаем таймер:
            // AvaloniaEdit фильтрует список на лету
            if (_completionWindow != null) return;

            char c = e.Text[0];

            // Автодополнение срабатывает при наборе букв, цифр, знака подчеркивания или триггерных символов
            if (char.IsLetterOrDigit(c) || c is '_' or '!' or '-' or ':')
            {
                // Сбрасываем и перезапускаем таймер задержки (400мс).
                // При непрерывном наборе или зажатии клавиши (ffffffffff) таймер постоянно сбрасывается
                // и не блокирует повтор ввода в системе. Окно появится только после паузы в 400мс.
                _completionTimer.Stop();
                _completionTimer.Start();
            }
            else if (c == ' ')
            {
                // Проверяем, стоит ли пробел после триггера списка ('-') или ключа (':')
                var doc = AssociatedObject.Document;
                int caret = AssociatedObject.TextArea.Caret.Offset;
                if (doc != null && caret >= 2)
                {
                    char prev = doc.GetCharAt(caret - 2);
                    if (prev is '-' or ':')
                    {
                        _completionTimer.Stop();
                        _completionTimer.Start();
                        return;
                    }
                }
                _completionTimer.Stop();
            }
            else
            {
                _completionTimer.Stop();
            }
        }

        public void ShowCompletion(bool forced)
        {
            if (AssociatedObject?.TextArea == null || AssociatedObject.Document == null) return;
            if (AssociatedObject.IsReadOnly) return;

            var textArea = AssociatedObject.TextArea;
            var doc = AssociatedObject.Document;
            int caretOffset = textArea.Caret.Offset;
            if (caretOffset < 0 || caretOffset > doc.TextLength) return;

            var caretLine = doc.GetLineByOffset(caretOffset);
            var textBeforeCaretOnLine = doc.GetText(caretLine.Offset, caretOffset - caretLine.Offset);
            var trimmedBefore = textBeforeCaretOnLine.TrimStart();
            bool isListLine = trimmedBefore.StartsWith("-");

            int startOffset = caretOffset;
            string currentWord = "";

            if (isListLine)
            {
                // Для строк списка (например, "  -", "  - ", "  - !", "  - !res", "  - top")
                // Находим позицию дефиса и начало значения после него
                int dashIndex = textBeforeCaretOnLine.IndexOf('-');
                int valueStartIndex = dashIndex + 1;
                while (valueStartIndex < textBeforeCaretOnLine.Length && textBeforeCaretOnLine[valueStartIndex] == ' ')
                {
                    valueStartIndex++;
                }

                int valueStartOffset = caretLine.Offset + valueStartIndex;

                if (caretOffset <= valueStartOffset)
                {
                    // Курсор стоит на дефисе или сразу после него/пробелов ("  -" или "  - ")
                    startOffset = caretOffset;
                    currentWord = "";
                }
                else
                {
                    // Пользователь уже набирает слово после дефиса (например, "!" или имя тега/файла)
                    startOffset = caretOffset;
                    while (startOffset > valueStartOffset)
                    {
                        char prev = doc.GetCharAt(startOffset - 1);
                        if (char.IsLetterOrDigit(prev) || prev is '_' or '-' or '!')
                        {
                            startOffset--;
                        }
                        else
                        {
                            break;
                        }
                    }
                    currentWord = doc.GetText(startOffset, caretOffset - startOffset);
                }
            }
            else
            {
                // Обычная строка (ключ или значение свойства)
                while (startOffset > 0)
                {
                    char prev = doc.GetCharAt(startOffset - 1);
                    if (char.IsLetterOrDigit(prev) || prev is '_' or '-' or '!')
                    {
                        startOffset--;
                    }
                    else
                    {
                        break;
                    }
                }
                currentWord = doc.GetText(startOffset, caretOffset - startOffset);
            }

            // Если автодополнение не вызвано принудительно (Ctrl+Space), не открываем для пустых слов без спец. триггера
            if (!forced && string.IsNullOrEmpty(currentWord))
            {
                bool isTrigger = false;
                if (_triggerOnNewLine)
                {
                    isTrigger = true;
                }
                else if (isListLine)
                {
                    var afterTrim = trimmedBefore.TrimEnd();
                    if (afterTrim == "-")
                        isTrigger = true;
                }

                if (!isTrigger && caretOffset > 0)
                {
                    char lastChar = doc.GetCharAt(caretOffset - 1);
                    if (lastChar is ':' or '.' or '-' or '!')
                        isTrigger = true;
                }

                if (!isTrigger)
                {
                    _triggerOnNewLine = false;
                    return;
                }
            }

            _triggerOnNewLine = false;

            var completions = CompletionProvider.GetCompletions(AssociatedObject, currentWord, caretOffset);
            if (completions == null || completions.Count == 0) return;

            _completionWindow?.Close();

            _completionWindow = new QuartzCompletionWindow(textArea)
            {
                StartOffset = startOffset,
                FontFamily = AssociatedObject.FontFamily
            };

            _completionWindow.CompletionList.FontFamily = AssociatedObject.FontFamily;
            _completionWindow.CompletionList.IsFiltering = true;

            foreach (var item in completions)
            {
                _completionWindow.CompletionList.CompletionData.Add(item);
            }

            _completionWindow.Closed += (s, e) => _completionWindow = null;

            _completionWindow.Show();

            if (!string.IsNullOrEmpty(currentWord))
            {
                _completionWindow.CompletionList.SelectItem(currentWord);
            }

            if (_completionWindow.CompletionList.SelectedItem == null)
            {
                _completionWindow.SelectFirstItem();
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnEditorLoaded;
                AssociatedObject.DataContextChanged -= OnDataContextChanged;
                AssociatedObject.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
                AssociatedObject.PropertyChanged -= OnPropertyChanged;
                AssociatedObject.GestureRecognizers.Remove(_pinchRecognizer);
                AssociatedObject.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
                AssociatedObject.RemoveHandler(InputElement.KeyDownEvent, OnEditorKeyDown);
                AssociatedObject.RemoveHandler(InputElement.KeyUpEvent, OnEditorKeyUp);
                AssociatedObject.RemoveHandler(InputElement.PinchEvent, OnPinchGesture);
                AssociatedObject.RemoveHandler(InputElement.PinchEndedEvent, OnPinchGestureEnded);
                AssociatedObject.RemoveHandler(InputElement.PointerTouchPadGestureMagnifyEvent, OnTouchPadGestureMagnify);

                LinuxX11PinchGestureService.PinchGesture -= OnLinuxPinchGesture;

                if (AssociatedObject.TextArea != null)
                {
                    if (AssociatedObject.TextArea.Caret != null)
                    {
                        AssociatedObject.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
                    }
                    AssociatedObject.TextArea.TextEntered -= OnTextAreaTextEntered;
                    AssociatedObject.TextArea.LostFocus -= OnTextAreaLostFocus;
                    AssociatedObject.TextArea.RemoveHandler(
                        InputElement.PointerPressedEvent,
                        OnTextAreaPointerPressed);
                }

                _completionTimer.Stop();
                _completionWindow?.Close();
                _completionWindow = null;

                HookDocument(null);
                HookVm(null);

                if (AssociatedObject.TextArea?.TextView != null && _indentationRenderer != null)
                {
                    AssociatedObject.TextArea.TextView.BackgroundRenderers.Remove(_indentationRenderer);
                    _indentationRenderer = null;
                }
            }

            if (Avalonia.Application.Current != null)
            {
                Avalonia.Application.Current.ActualThemeVariantChanged -= OnGlobalThemeChanged;
            }

            RemoveTextMate();
            _registryOptions = null;
            _currentScope = null;

            base.OnDetaching();
        }

        public void NavigateTo(long line, long column)
        {
            if (AssociatedObject == null) return;

            void DoNavigate(int retriesLeft)
            {
                if (AssociatedObject == null) return;
                var doc = AssociatedObject.Document;
                if (doc == null || doc.LineCount == 0)
                {
                    if (retriesLeft > 0)
                    {
                        Dispatcher.UIThread.Post(() => DoNavigate(retriesLeft - 1), DispatcherPriority.Background);
                    }
                    return;
                }

                var scrollViewer = AssociatedObject.FindDescendantOfType<ScrollViewer>();
                if ((scrollViewer == null || scrollViewer.Viewport.Height <= 0 || !AssociatedObject.IsEffectivelyVisible) && retriesLeft > 0)
                {
                    Dispatcher.UIThread.Post(() => DoNavigate(retriesLeft - 1), DispatcherPriority.Background);
                    return;
                }

                int targetLine = Math.Clamp((int)line, 1, Math.Max(1, doc.LineCount));
                var docLine = doc.GetLineByNumber(targetLine);
                int targetCol = Math.Clamp((int)column, 1, Math.Max(1, docLine.Length + 1));
                int offset = doc.GetOffset(targetLine, targetCol);

                AssociatedObject.CaretOffset = offset;
                if (AssociatedObject.TextArea != null)
                {
                    AssociatedObject.TextArea.Caret.Location = new TextLocation(targetLine, targetCol);
                    AssociatedObject.TextArea.ClearSelection();
                }

                AssociatedObject.ScrollTo(targetLine, targetCol);

                if (AssociatedObject.TextArea?.Caret != null)
                {
                    AssociatedObject.TextArea.Caret.BringCaretToView();
                }

                AssociatedObject.Focus();
                AssociatedObject.TextArea?.Focus();

                if (scrollViewer != null)
                {
                    ScrollProps.SetVerticalOffset(AssociatedObject, scrollViewer.Offset.Y);
                    ScrollProps.SetHorizontalOffset(AssociatedObject, scrollViewer.Offset.X);
                }
            }

            var sv = AssociatedObject.FindDescendantOfType<ScrollViewer>();
            if (sv != null && sv.Viewport.Height > 0 && AssociatedObject.IsEffectivelyVisible)
            {
                DoNavigate(0);
            }

            Dispatcher.UIThread.Post(() => DoNavigate(10), DispatcherPriority.Background);
        }
    }

    public class YamlIndentationRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private Pen _linePen = new(new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)), 1.0);

        public YamlIndentationRenderer(TextEditor editor) => _editor = editor;
        public KnownLayer Layer => KnownLayer.Background;

        public void UpdateLineColor(Color color)
        {
            _linePen = new Pen(new SolidColorBrush(color), 1.0);
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor.Document == null) return;
            if (!textView.VisualLinesValid) return;

            var options = _editor.Options;
            int indentationSize = options.IndentationSize;
            if (indentationSize <= 0) indentationSize = 2;

            foreach (VisualLine visualLine in textView.VisualLines)
            {
                DocumentLine docLine = visualLine.FirstDocumentLine;
                int lineLen = docLine.Length;
                if (lineLen > 1000) continue; // Защита от гигантских строк

                int lineOffset = docLine.Offset;
                int leadingSpaces = 0;
                for (int i = 0; i < lineLen; i++)
                {
                    char c = _editor.Document.GetCharAt(lineOffset + i);
                    if (c == ' ') leadingSpaces++;
                    else if (c == '\t') leadingSpaces += indentationSize;
                    else break;
                }

                // Линии отступов рисуются строго внутри отступа (пробелов) ДО первой буквы (i < leadingSpaces),
                // чтобы буквы не перекрывали вертикальную линию (например, перед width:).
                for (int i = indentationSize; i < leadingSpaces; i += indentationSize)
                {
                    if (i >= visualLine.VisualLength) break;

                    var position = visualLine.GetVisualPosition(i, VisualYPosition.TextTop);
                    double x = Math.Round(position.X - textView.ScrollOffset.X) + 0.5;

                    double topY = visualLine.VisualTop - textView.ScrollOffset.Y;
                    double bottomY = topY + visualLine.Height;

                    drawingContext.DrawLine(_linePen, new Point(x, topY), new Point(x, bottomY));
                }
            }
        }
    }

    public class YamlIndentationStrategy : IIndentationStrategy
    {
        private readonly TextEditor _editor;

        public YamlIndentationStrategy(TextEditor editor) => _editor = editor;

        public void IndentLine(TextDocument document, DocumentLine line)
        {
            if (document == null || line == null) return;
            var previousLine = line.PreviousLine;
            if (previousLine == null) return;

            string prevText = document.GetText(previousLine.Offset, previousLine.Length);
            string prevTrimmed = prevText.Trim();
            if (prevTrimmed.StartsWith("#") || string.IsNullOrWhiteSpace(prevTrimmed))
            {
                for (var p = previousLine.PreviousLine; p != null; p = p.PreviousLine)
                {
                    var t = document.GetText(p.Offset, p.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(t) && !t.StartsWith("#"))
                    {
                        prevText = document.GetText(p.Offset, p.Length);
                        prevTrimmed = t;
                        break;
                    }
                }
            }

            int prevIndent = 0;
            while (prevIndent < prevText.Length && (prevText[prevIndent] == ' ' || prevText[prevIndent] == '\t'))
            {
                prevIndent++;
            }

            int indentSize = _editor.Options?.IndentationSize ?? 2;
            if (indentSize <= 0) indentSize = 2;
            int newIndent = prevIndent;

            if (prevTrimmed.EndsWith(':') ||
                prevTrimmed.StartsWith("- !") ||
                (prevTrimmed.StartsWith("- ") && prevTrimmed.Contains(':')))
            {
                newIndent = prevIndent + indentSize;
            }

            string indentString = new string(' ', newIndent);
            string currentLineText = document.GetText(line.Offset, line.Length);
            int currentIndentLen = 0;
            while (currentIndentLen < currentLineText.Length && (currentLineText[currentIndentLen] == ' ' || currentLineText[currentIndentLen] == '\t'))
            {
                currentIndentLen++;
            }

            document.Replace(line.Offset, currentIndentLen, indentString);
        }

        public void IndentLines(TextDocument document, int begin, int end)
        {
            for (int i = begin; i <= end; i++)
            {
                IndentLine(document, document.GetLineByNumber(i));
            }
        }
    }
}