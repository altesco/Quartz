using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace Quartz.UI.Controls;

public class FillLastInRowPanel : Panel
{
    // 1. Измеряем, сколько места нужно элементам (оставляем без изменений)
    protected override Size MeasureOverride(Size availableSize)
    {
        double rowWidth = 0;
        double rowHeight = 0;
        double totalHeight = 0;
        double maxWidth = 0;

        foreach (var child in Children)
        {
            child.Measure(availableSize);

            if (rowWidth + child.DesiredSize.Width > availableSize.Width && rowWidth > 0)
            {
                totalHeight += rowHeight;
                maxWidth = Math.Max(maxWidth, rowWidth);
                rowWidth = 0;
                rowHeight = 0;
            }

            rowWidth += child.DesiredSize.Width;
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        totalHeight += rowHeight;
        maxWidth = Math.Max(maxWidth, rowWidth);

        return new Size(
            double.IsInfinity(availableSize.Width) ? maxWidth : availableSize.Width,
            totalHeight);
    }

    // 2. Расставляем элементы по местам
    protected override Size ArrangeOverride(Size finalSize)
    {
        double currentY = 0;
        var currentRow = new List<Control>();
        double currentRowWidth = 0;
        double currentRowHeight = 0;

        foreach (var child in Children)
        {
            if (currentRowWidth + child.DesiredSize.Width > finalSize.Width && currentRow.Count > 0)
            {
                // Это промежуточная строка -> передаем isLastRow = false
                ArrangeRow(currentRow, currentY, currentRowHeight, finalSize.Width, currentRowWidth, isLastRow: false);
                currentY += currentRowHeight;
                currentRow.Clear();
                currentRowWidth = 0;
                currentRowHeight = 0;
            }

            currentRow.Add(child);
            currentRowWidth += child.DesiredSize.Width;
            currentRowHeight = Math.Max(currentRowHeight, child.DesiredSize.Height);
        }

        // Рендерим самую последнюю строку -> передаем isLastRow = true
        if (currentRow.Count > 0)
        {
            ArrangeRow(currentRow, currentY, currentRowHeight, finalSize.Width, currentRowWidth, isLastRow: true);
        }

        return finalSize;
    }

    // Добавили флаг isLastRow в параметры
    private void ArrangeRow(List<Control> row, double y, double rowHeight, double totalWidth, double rowWidth,
        bool isLastRow)
    {
        double currentX = 0;
        for (int i = 0; i < row.Count; i++)
        {
            var child = row[i];
            double width = child.DesiredSize.Width;

            // Растягиваем только если это конец строки И строка НЕ последняя в панели
            if (i == row.Count - 1 && !isLastRow)
            {
                width += totalWidth - rowWidth;
            }

            // Выставляем псевдоклассы в зависимости от индекса элемента в текущей строке
            child.Classes.Set("first-child", i == 0);
            child.Classes.Set("last-child", i == row.Count - 1);

            child.Arrange(new Rect(currentX, y, width, rowHeight));
            currentX += width;
        }
    }
}