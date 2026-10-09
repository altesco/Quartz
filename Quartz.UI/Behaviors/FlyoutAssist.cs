using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Quartz.UI.Behaviors
{
    public class FlyoutAssist
    {
        // Регистрируем Attached Property для сдвига по X
        public static readonly AttachedProperty<double> HorizontalOffsetProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("HorizontalOffset");

        // Регистрируем Attached Property для сдвига по Y
        public static readonly AttachedProperty<double> VerticalOffsetProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("VerticalOffset");

        public static void SetHorizontalOffset(AvaloniaObject element, double value) => element.SetValue(HorizontalOffsetProperty, value);
        public static double GetHorizontalOffset(AvaloniaObject element) => element.GetValue(HorizontalOffsetProperty);

        public static void SetVerticalOffset(AvaloniaObject element, double value) => element.SetValue(VerticalOffsetProperty, value);
        public static double GetVerticalOffset(AvaloniaObject element) => element.GetValue(VerticalOffsetProperty);

        static FlyoutAssist()
        {
            HorizontalOffsetProperty.Changed.AddClassHandler<Control>(OnOffsetChanged);
            VerticalOffsetProperty.Changed.AddClassHandler<Control>(OnOffsetChanged);
        }

        private static void OnOffsetChanged(Control element, AvaloniaPropertyChangedEventArgs e)
        {
            ApplyOffset(element);

            element.AttachedToLogicalTree += (s, ev) => ApplyOffset(element);
            element.AttachedToVisualTree += (s, ev) => ApplyOffset(element);
            element.Loaded += (s, ev) => ApplyOffset(element);
        }

        private static void ApplyOffset(Control element)
        {
            var popup = element.Parent as Popup ?? element.FindAncestorOfType<Popup>();
            if (popup == null) return;

            if (element.IsSet(HorizontalOffsetProperty))
            {
                popup.HorizontalOffset = GetHorizontalOffset(element);
            }

            if (element.IsSet(VerticalOffsetProperty))
            {
                popup.VerticalOffset = GetVerticalOffset(element);
            }
        }
    }
}