using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
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

        public const int MaxLineLengthForHighlighting = 500;
        private const double MinFontSize = 6;
        private const double MaxFontSize = 72;
        private const double ZoomStep = 1.0;

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

            AssociatedObject.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel);
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextEditor.DocumentProperty)
            {
                HookDocument(e.GetNewValue<TextDocument?>());
                ConfigureEditorForCurrentFile();
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
        }

        private void OnDataContextChanged(object? sender, EventArgs e) => ConfigureEditorForCurrentFile();

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

            // 1. Управление YamlIndentationRenderer (только для YAML файлов и если нет сверхдлинных строк)
            if (isYaml && !hasExcessiveLines)
            {
                if (_indentationRenderer == null && AssociatedObject.TextArea?.TextView != null)
                {
                    _indentationRenderer = new YamlIndentationRenderer(AssociatedObject);
                    AssociatedObject.TextArea.TextView.BackgroundRenderers.Add(_indentationRenderer);
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

            // 4. Линии YAML
            if (_indentationRenderer != null && TryGetThemeColor("BorderColor60", targetTheme, out var borderColor))
            {
                _indentationRenderer.UpdateLineColor(borderColor);
            }

            textView.Redraw();
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

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;

                if (Math.Abs(e.Delta.Y) < 0.001) return;

                double direction = Math.Sign(e.Delta.Y);
                double currentFontSize = AssociatedObject!.FontSize;
                double newFontSize = currentFontSize + (direction * ZoomStep);

                if (newFontSize >= MinFontSize && newFontSize <= MaxFontSize)
                {
                    AssociatedObject.FontSize = newFontSize;
                }
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
                AssociatedObject.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);

                HookDocument(null);

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
    }

    public class YamlIndentationRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private Pen _linePen = new(Brushes.Transparent, 1.0);

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
                    else break;
                }

                for (int i = indentationSize; i <= leadingSpaces; i += indentationSize)
                {
                    var position = visualLine.GetVisualPosition(i, VisualYPosition.TextTop);
                    double x = position.X - textView.ScrollOffset.X;

                    double topY = visualLine.VisualTop - textView.ScrollOffset.Y;
                    double bottomY = topY + visualLine.Height;

                    drawingContext.DrawLine(_linePen, new Point(x, topY), new Point(x, bottomY));
                }
            }
        }
    }
}