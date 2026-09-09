using System;
using Avalonia;
using Avalonia.Controls;

namespace Quartz.UI.Controls;

public class ChromeTabsPanel : Panel
{
    private const double MaxTabWidth = 180;
    private const double MinTabWidth = 0;

    // 1. Измеряем вкладки с учетом РЕАЛЬНО доступного места
    protected override Size MeasureOverride(Size availableSize)
    {
        double maxChildHeight = 0;
        int count = Children.Count;

        if (count == 0) return new Size(0, maxChildHeight);

        // Считаем, сколько чистой ширины достанется ОДНОЙ вкладке прямо сейчас
        double slotWidth = double.IsInfinity(availableSize.Width)
            ? MaxTabWidth
            : Math.Clamp(availableSize.Width / count, MinTabWidth, MaxTabWidth);

        foreach (var child in Children)
        {
            // Передаем вкладке её реальный будущий размер.
            // Теперь TextBlock внутри вкладки сразу увидит ограничения!
            child.Measure(new Size(slotWidth, availableSize.Height));
            maxChildHeight = Math.Max(maxChildHeight, child.DesiredSize.Height);
        }

        double totalWidth = double.IsInfinity(availableSize.Width)
            ? count * MaxTabWidth
            : Math.Min(count * MaxTabWidth, availableSize.Width);

        return new Size(totalWidth, maxChildHeight);
    }

    // 2. Расставляем строго по равным слотам
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0) return finalSize;

        double calculatedWidth = finalSize.Width / Children.Count;
        double itemWidth = Math.Clamp(calculatedWidth, MinTabWidth, MaxTabWidth);

        double x = 0;
        foreach (var child in Children)
        {
            // Поскольку у TabItem теперь стоит HorizontalAlignment="Stretch",
            // он идеально ужмется в эти границы, а текст красиво сократится.
            child.Arrange(new Rect(x, 0, itemWidth, finalSize.Height));
            x += itemWidth;
        }

        return finalSize;
    }
}