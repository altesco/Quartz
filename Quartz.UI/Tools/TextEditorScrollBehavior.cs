using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using System;

namespace Quartz.UI.Tools;

public static class ScrollProps
{
    public static readonly AttachedProperty<double> VerticalOffsetProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, double>(
            "VerticalOffset", typeof(ScrollProps), double.NaN, defaultBindingMode: BindingMode.TwoWay);

    public static double GetVerticalOffset(TextEditor element)
    {
        var val = element.GetValue(VerticalOffsetProperty);
        return double.IsNaN(val) ? 0.0 : val;
    }

    public static void SetVerticalOffset(TextEditor element, double value) =>
        element.SetValue(VerticalOffsetProperty, value);

    public static readonly AttachedProperty<double> HorizontalOffsetProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, double>(
            "HorizontalOffset", typeof(ScrollProps), double.NaN, defaultBindingMode: BindingMode.TwoWay);

    public static double GetHorizontalOffset(TextEditor element)
    {
        var val = element.GetValue(HorizontalOffsetProperty);
        return double.IsNaN(val) ? 0.0 : val;
    }

    public static void SetHorizontalOffset(TextEditor element, double value) =>
        element.SetValue(HorizontalOffsetProperty, value);

    private static readonly AttachedProperty<bool> IsSyncingProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("IsSyncing", typeof(ScrollProps), false);

    private static readonly AttachedProperty<bool> IsSubscribedProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("IsSubscribed", typeof(ScrollProps), false);

    private static readonly AttachedProperty<IDisposable?> SubscriptionTokenProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, IDisposable?>("SubscriptionToken", typeof(ScrollProps), null);

    static ScrollProps()
    {
        // 1. Избавляемся от капризного Subscribe и используем AddClassHandler
        VerticalOffsetProperty.Changed.AddClassHandler<TextEditor>((editor, e) =>
            OnTargetOffsetChanged(editor, e.GetNewValue<double>()));
        HorizontalOffsetProperty.Changed.AddClassHandler<TextEditor>((editor, e) =>
            OnTargetOffsetChanged(editor, e.GetNewValue<double>()));
    }

    private static void OnTargetOffsetChanged(TextEditor editor, double newValue)
    {
        if (double.IsNaN(newValue) || editor.GetValue(IsSyncingProperty)) return;

        EnsureSubscribed(editor);
        TriggerRestoration(editor);
    }

    private static void EnsureSubscribed(TextEditor editor)
    {
        if (editor.GetValue(IsSubscribedProperty)) return;
        editor.SetValue(IsSubscribedProperty, true);

        editor.TemplateApplied += (s, e) => HookScrollViewer(editor);
        HookScrollViewer(editor);
    }

    private static void HookScrollViewer(TextEditor editor)
    {
        var scrollViewer = editor.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer == null) return;

        editor.GetValue(SubscriptionTokenProperty)?.Dispose();

        // 2. Вместо реактивного GetObservable вешаем подписку через стандартное событие ScrollChanged
        EventHandler<ScrollChangedEventArgs> scrollHandler = (s, args) =>
        {
            if (editor.GetValue(IsSyncingProperty)) return;

            if (scrollViewer.Viewport.Height <= 0 || scrollViewer.Viewport.Width <= 0 || !editor.IsEffectivelyVisible)
                return;

            editor.SetValue(IsSyncingProperty, true);
            try
            {
                double curV = GetVerticalOffset(editor);
                double curH = GetHorizontalOffset(editor);
                var offset = scrollViewer.Offset;

                // 3. Исправляем "Ambiguous invocation", явно указывая generic-тип <double> в SetValue
                if (Math.Abs(curV - offset.Y) > 0.5)
                    editor.SetValue<double>(VerticalOffsetProperty, offset.Y);

                if (Math.Abs(curH - offset.X) > 0.5)
                    editor.SetValue<double>(HorizontalOffsetProperty, offset.X);
            }
            finally
            {
                editor.SetValue(IsSyncingProperty, false);
            }
        };

        scrollViewer.ScrollChanged += scrollHandler;

        // Создаем токен отписки
        var token = new ScrollSubscriptionToken(() => scrollViewer.ScrollChanged -= scrollHandler);
        editor.SetValue<IDisposable?>(SubscriptionTokenProperty, token);
    }

    private static void TriggerRestoration(TextEditor editor)
    {
        editor.SetValue(IsSyncingProperty, true);

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var scrollViewer = editor.FindDescendantOfType<ScrollViewer>();
                if (scrollViewer == null || !editor.IsEffectivelyVisible || scrollViewer.Viewport.Height <= 0)
                {
                    if (editor.IsAttachedToVisualTree()) TriggerRestoration(editor);
                    return;
                }

                double targetY = GetVerticalOffset(editor);
                double targetX = GetHorizontalOffset(editor);

                if (targetY > 0 && scrollViewer.Extent.Height <= scrollViewer.Viewport.Height + 5)
                {
                    editor.InvalidateMeasure();
                    editor.InvalidateArrange();
                    TriggerRestoration(editor);
                    return;
                }

                scrollViewer.Offset = new Vector(targetX, targetY);
            }
            finally
            {
                Dispatcher.UIThread.Post(() => { editor.SetValue(IsSyncingProperty, false); },
                    DispatcherPriority.Background);
            }
        }, DispatcherPriority.Background);
    }

    // Класс-помощник для детерминированной отписки от события скролла
    private class ScrollSubscriptionToken : IDisposable
    {
        private readonly Action _dispose;
        public ScrollSubscriptionToken(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}
