using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using Quartz.Core.Models;

namespace Quartz.UI.Tools;

public class ErrorMarkerRenderer : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Selection;

    // Храним сырые проблемы из ViewModel, а не сегменты
    public List<Problem> Problems { get; } = new();
    public List<EditorError> Errors => Problems.OfType<EditorError>().ToList();

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        // Если проблем нет или документ еще не загрузился — рисовать нечего
        if (Problems.Count == 0 || textView.Document == null || textView.Document.TextLength == 0) return;

        var errorBrush = GetBrush("ErrorColor", Brushes.Red);
        var warningBrush = GetBrush("WarningColor", Brushes.Orange);
        var errorPen = new Pen(errorBrush, 1.0);
        var warningPen = new Pen(warningBrush, 1.0);

        foreach (var problem in Problems)
        {
            var pen = problem is EditorWarning ? warningPen : errorPen;
            try
            {
                // Считаем смещение прямо в момент отрисовки — тут документ уже точно готов!
                var startOffset = textView.Document.GetOffset(new TextLocation((int)problem.Line, (int)problem.Column));
                var length = Math.Min(problem.Length, textView.Document.TextLength - startOffset);

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

    private static IBrush GetBrush(string resourceKey, IBrush fallback)
    {
        if (Avalonia.Application.Current != null &&
            Avalonia.Application.Current.TryFindResource(resourceKey, out var res) &&
            res is Color col)
        {
            return new SolidColorBrush(col);
        }
        return fallback;
    }
}