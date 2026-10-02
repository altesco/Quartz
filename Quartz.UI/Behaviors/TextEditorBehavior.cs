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

        private const double MinFontSize = 6;
        private const double MaxFontSize = 72;
        private const double ZoomStep = 1.0;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is null) return;

            try
            {
                // Берем тему строго из приложения, так как DataTemplate может врать
                var isLight = Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Light;
                var initialTheme = isLight ? ThemeName.LightPlus : ThemeName.DarkPlus;

                _registryOptions = new RegistryOptions(initialTheme);
                _textMateInstallation = AssociatedObject.InstallTextMate(_registryOptions);

                string scopeName = _registryOptions.GetScopeByExtension(".yaml");
                if (!string.IsNullOrEmpty(scopeName))
                {
                    _textMateInstallation.SetGrammar(scopeName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextMate init failed: {ex.Message}");
            }

            try
            {
                if (AssociatedObject.TextArea?.TextView != null)
                {
                    _indentationRenderer = new YamlIndentationRenderer(AssociatedObject);
                    AssociatedObject.TextArea.TextView.BackgroundRenderers.Add(_indentationRenderer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Indentation renderer failed: {ex.Message}");
            }

            // Подписки
            AssociatedObject.Loaded += OnEditorLoaded;
            AssociatedObject.ActualThemeVariantChanged += OnActualThemeVariantChanged;

            if (Avalonia.Application.Current != null)
            {
                Avalonia.Application.Current.ActualThemeVariantChanged += OnGlobalThemeChanged;
            }

            UpdateThemeColors();

            AssociatedObject.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel);
        }

        private void OnEditorLoaded(object? sender, RoutedEventArgs e) => UpdateThemeColors();

        private void OnActualThemeVariantChanged(object? sender, EventArgs e) => UpdateThemeColors();

        private void OnGlobalThemeChanged(object? sender, EventArgs e) => UpdateThemeColors();

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
                AssociatedObject.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
                AssociatedObject.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);

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

            _textMateInstallation?.Dispose();
            _textMateInstallation = null;
            _registryOptions = null;

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
                string text = _editor.Document.GetText(docLine.Offset, docLine.Length);

                int leadingSpaces = 0;
                foreach (char c in text)
                {
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