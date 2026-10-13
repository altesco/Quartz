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
            Control.LoadedEvent.AddClassHandler<ToolTip>((tip, _) => ApplyOffset(tip));
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

            popup.Opened -= OnPopupOpened;
            popup.Opened += OnPopupOpened;

            ApplyPopupOffset(element, popup);
        }

        private static void OnPopupOpened(object? sender, System.EventArgs e)
        {
            if (sender is Popup popup && popup.Child is Control element)
            {
                ApplyPopupOffset(element, popup);
            }
        }

        private static void ApplyPopupOffset(Control element, Popup popup)
        {
            if (element is ToolTip)
            {
                var target = popup.PlacementTarget as Control;

                if (target != null && target.IsSet(ToolTip.VerticalOffsetProperty))
                {
                    popup.VerticalOffset = ToolTip.GetVerticalOffset(target);
                }
                else
                {
                    PlacementMode placement = PlacementMode.Bottom;

                    if (target != null && target.IsSet(ToolTip.PlacementProperty))
                    {
                        placement = ToolTip.GetPlacement(target);
                    }
                    else if (popup.Placement != PlacementMode.Pointer)
                    {
                        placement = popup.Placement;
                    }

                    switch (placement)
                    {
                        case PlacementMode.Right:
                        case PlacementMode.RightEdgeAlignedTop:
                        case PlacementMode.RightEdgeAlignedBottom:
                            popup.VerticalOffset = -6;
                            break;

                        case PlacementMode.Left:
                        case PlacementMode.LeftEdgeAlignedTop:
                        case PlacementMode.LeftEdgeAlignedBottom:
                            popup.VerticalOffset = -6;
                            break;

                        case PlacementMode.Top:
                        case PlacementMode.TopEdgeAlignedLeft:
                        case PlacementMode.TopEdgeAlignedRight:
                            popup.VerticalOffset = 12;
                            break;

                        case PlacementMode.Bottom:
                        case PlacementMode.BottomEdgeAlignedLeft:
                        case PlacementMode.BottomEdgeAlignedRight:
                        default:
                            popup.VerticalOffset = -12;
                            break;
                    }
                }

                if (target != null && target.IsSet(ToolTip.HorizontalOffsetProperty))
                {
                    popup.HorizontalOffset = ToolTip.GetHorizontalOffset(target);
                }
                else
                {
                    PlacementMode placement = PlacementMode.Bottom;

                    if (target != null && target.IsSet(ToolTip.PlacementProperty))
                    {
                        placement = ToolTip.GetPlacement(target);
                    }
                    else if (popup.Placement != PlacementMode.Pointer)
                    {
                        placement = popup.Placement;
                    }

                    switch (placement)
                    {
                        case PlacementMode.Right:
                        case PlacementMode.RightEdgeAlignedTop:
                        case PlacementMode.RightEdgeAlignedBottom:
                            popup.HorizontalOffset = -6;
                            break;

                        case PlacementMode.Left:
                        case PlacementMode.LeftEdgeAlignedTop:
                        case PlacementMode.LeftEdgeAlignedBottom:
                            popup.HorizontalOffset = 6;
                            break;

                        default:
                            popup.HorizontalOffset = 0;
                            break;
                    }
                }

                return;
            }

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