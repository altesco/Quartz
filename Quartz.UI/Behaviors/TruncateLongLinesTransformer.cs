using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Controls;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Quartz.UI.Behaviors
{
    public class TruncateLongLinesTransformer : IVisualLineTransformer
    {
        private static readonly Action<VisualLineElement, VisualLineElementTextRunProperties> SetTextRunPropertiesAction =
            (Action<VisualLineElement, VisualLineElementTextRunProperties>)Delegate.CreateDelegate(
                typeof(Action<VisualLineElement, VisualLineElementTextRunProperties>),
                typeof(VisualLineElement).GetMethod("SetTextRunProperties", BindingFlags.Instance | BindingFlags.NonPublic)!
            );

        private readonly HashSet<DocumentLine> _unfoldedLines = new();
        private readonly Dictionary<DocumentLine, Button> _buttons = new();

        /// <summary>
        /// Порог длины строки (в символах), после которого хвост строки визуально усекается.
        /// </summary>
        public int MaxVisualLineLength { get; set; } = 1000;

        /// <summary>
        /// Сброс списка раскрытых строк и кэша кнопок (например, при смене файла или документа).
        /// </summary>
        public void Reset()
        {
            _unfoldedLines.Clear();
            _buttons.Clear();
        }

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
        {
            var visualLine = context.VisualLine;
            var docLine = visualLine.FirstDocumentLine;

            if (docLine.Length <= MaxVisualLineLength || _unfoldedLines.Contains(docLine))
                return;

            int totalDocLength = 0;
            for (int i = 0; i < elements.Count; i++)
            {
                totalDocLength += elements[i].DocumentLength;
            }

            if (totalDocLength <= MaxVisualLineLength)
                return;

            int remainingLength = totalDocLength - MaxVisualLineLength;
            var textView = context.TextView;

            if (!_buttons.TryGetValue(docLine, out var button))
            {
                button = new Button
                {
                    Classes = { "LongLineTruncateBadge" },
                    Focusable = false
                };

                ToolTip.SetTip(button, "Отрисовка приостановлена для длинной строки из соображений производительности.");

                button.Click += (s, e) =>
                {
                    _unfoldedLines.Add(docLine);
                    _buttons.Remove(docLine);
                    textView.Redraw();
                };

                _buttons[docLine] = button;
            }

            string contentText = $"Показать больше ({remainingLength:N0} сим.)";
            if (!object.Equals(button.Content, contentText))
            {
                button.Content = contentText;
            }

            var firstPart = new VisualLineText(visualLine, MaxVisualLineLength);
            var badgePart = new InlineObjectElement(remainingLength, button);

            var runProps = new VisualLineElementTextRunProperties(context.GlobalTextRunProperties);
            SetTextRunPropertiesAction(firstPart, runProps);
            SetTextRunPropertiesAction(badgePart, runProps);

            visualLine.ReplaceElement(0, elements.Count, new VisualLineElement[] { firstPart, badgePart });
        }
    }
}
