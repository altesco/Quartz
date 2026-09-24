using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity; // Важно: для RoutingStrategies
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
        private TextMate.Installation? _textMateInstallation;
        private IBackgroundRenderer? _indentationRenderer;

        // Константы для масштабирования
        private const double MinFontSize = 6;
        private const double MaxFontSize = 72;
        private const double ZoomStep = 1.0;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is null) return;

            // Базовые настройки редактора
            AssociatedObject.Options.ConvertTabsToSpaces = true;
            AssociatedObject.Options.IndentationSize = 4;
            AssociatedObject.Options.HighlightCurrentLine = true;

            // ИСПРАВЛЕНО: Перехватываем событие колесика на стадии ТУННЕЛИРОВАНИЯ (Tunnel).
            // Это аналог Preview-событий. Мы заберем ввод до того, как его поглотит встроенный скролл.
            AssociatedObject.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel);

            // Подключаем кастомный рендерер линий отступа
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

            // Инициализация TextMate для подсветки синтаксиса YAML
            try
            {
                var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                _textMateInstallation = AssociatedObject.InstallTextMate(registryOptions);

                string scopeName = registryOptions.GetScopeByExtension(".yaml");
                if (!string.IsNullOrEmpty(scopeName))
                {
                    _textMateInstallation.SetGrammar(scopeName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextMate init failed: {ex.Message}");
            }
        }

        // ОБРАБОТЧИК МАСШТАБИРОВАНИЯ ТЕКСТА
        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Проверяем, что зажат именно CTRL
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Сообщаем системе, что мы обработали событие. Внутренний скролл больше не сработает!
                e.Handled = true;

                if (Math.Abs(e.Delta.Y) < 0.001) return;

                // Определяем направление: вверх (+1) или вниз (-1)
                double direction = Math.Sign(e.Delta.Y);

                double currentFontSize = AssociatedObject!.FontSize;
                double newFontSize = currentFontSize + (direction * ZoomStep);

                // Изменяем шрифт в рамках допустимого диапазона
                if (newFontSize >= MinFontSize && newFontSize <= MaxFontSize)
                {
                    AssociatedObject.FontSize = newFontSize;
                }
            }
        }

        protected override void OnDetaching()
        {
            // ИСПРАВЛЕНО: Правильно удаляем хэндлер стадии туннелирования
            if (AssociatedObject != null)
            {
                AssociatedObject.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            }

            if (AssociatedObject?.TextArea?.TextView != null && _indentationRenderer != null)
            {
                AssociatedObject.TextArea.TextView.BackgroundRenderers.Remove(_indentationRenderer);
                _indentationRenderer = null;
            }

            _textMateInstallation?.Dispose();
            _textMateInstallation = null;

            base.OnDetaching();
        }
    }

    /// <summary>
    /// Кастомный высокопроизводительный рендерер вертикальных линий отступов для AvaloniaEdit
    /// </summary>
    public class YamlIndentationRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private readonly Pen _linePen = new(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 1.0);

        public YamlIndentationRenderer(TextEditor editor) => _editor = editor;
        public KnownLayer Layer => KnownLayer.Background;

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
