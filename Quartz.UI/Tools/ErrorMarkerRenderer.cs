using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;
using Quartz.Core.Models;

namespace Quartz.UI.Tools;

public class ErrorMarkerRenderer : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Selection;

    // Храним сырые ошибки из ViewModel, а не сегменты
    public List<EditorError> Errors { get; } = new();

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        // Если ошибок нет или документ еще не загрузился — рисовать нечего
        if (Errors.Count == 0 || textView.Document == null || textView.Document.TextLength == 0) return;

        var pen = new Pen(Brushes.Red, 1.0);

        foreach (var error in Errors)
        {
            try
            {
                // Считаем смещение прямо в момент отрисовки — тут документ уже точно готов!
                var startOffset = textView.Document.GetOffset(new TextLocation((int)error.Line, (int)error.Column));
                var length = Math.Min(error.Length, textView.Document.TextLength - startOffset);

                if (startOffset < 0 || length <= 0) continue;

                var segment = new TextSegment { StartOffset = startOffset, Length = (int)length };

                // Рисуем волну для каждого прямоугольника текста
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open())
                    {
                        double startX = rect.Left;
                        double endX = rect.Right;
                        double baseY = rect.Bottom - 2;

                        context.BeginFigure(new Point(startX, baseY), false);

                        double step = 2.5;
                        double amplitude = 1.5;
                        bool toggleUp = true;

                        for (double x = startX + step; x < endX; x += step)
                        {
                            double currentY = baseY + (toggleUp ? amplitude : -amplitude);
                            context.LineTo(new Point(x, currentY));
                            toggleUp = !toggleUp;
                        }

                        context.LineTo(new Point(endX, baseY));
                    }

                    drawingContext.DrawGeometry(null, pen, geometry);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Защита от несовпадения индексов при быстром вводе текста
            }
        }
    }
}