using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
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

            // Обработчики для мгновенной настройки Popup тултипа ДО первого его открытия:
            Popup.ChildProperty.Changed.AddClassHandler<Popup>(OnPopupChildChanged);
            Popup.PlacementTargetProperty.Changed.AddClassHandler<Popup>(OnPopupPlacementTargetChanged);
            Popup.IsOpenProperty.Changed.AddClassHandler<Popup>(OnPopupIsOpenChanged);
            Control.LoadedEvent.AddClassHandler<ToolTip>((tip, _) => OnToolTipLoaded(tip));
        }

        private static void OnPopupChildChanged(Popup popup, AvaloniaPropertyChangedEventArgs e)
        {
            popup.ShouldUseOverlayLayer = true;
            if (e.NewValue is ToolTip tip || popup.Child is ToolTip)
            {
                ConfigureToolTipPopup(popup, (e.NewValue as ToolTip) ?? (popup.Child as ToolTip));
            }
        }

        private static void OnPopupPlacementTargetChanged(Popup popup, AvaloniaPropertyChangedEventArgs e)
        {
            popup.ShouldUseOverlayLayer = true;
            if (popup.Child is ToolTip tip)
            {
                ConfigureToolTipPopup(popup, tip);
            }
        }

        private static void OnPopupIsOpenChanged(Popup popup, AvaloniaPropertyChangedEventArgs e)
        {
            popup.ShouldUseOverlayLayer = true;
            if (e.NewValue is true && popup.Child is ToolTip tip)
            {
                ConfigureToolTipPopup(popup, tip);
            }
        }

        private static void OnToolTipLoaded(ToolTip tip)
        {
            tip.IsHitTestVisible = false;
            var popup = FindPopupFromToolTip(tip);
            if (popup != null)
            {
                ConfigureToolTipPopup(popup, tip);
            }
        }

        private static Popup? FindPopupFromToolTip(ToolTip tip)
        {
            if (tip.Parent is Popup p1) return p1;
            if (((ILogical)tip).LogicalParent is Popup p2) return p2;
            if (tip.Parent is PopupRoot pr && pr.Parent is Popup p3) return p3;
            if (TopLevel.GetTopLevel(tip) is PopupRoot pr2 && pr2.Parent is Popup p4) return p4;
            return tip.FindLogicalAncestorOfType<Popup>() ?? tip.FindAncestorOfType<Popup>();
        }

        public static void ConfigureToolTipPopup(Popup popup, ToolTip? tip = null)
        {
            tip ??= popup.Child as ToolTip;

            // Принудительно включаем OverlayLayer для ToolTip ВСЕГДА ДО первого открытия!
            // Это предотвращает создание отдельного нативного окна X11/Wayland под курсором,
            // из-за которого кнопка теряла ховер (PointerLeave) и тултип сразу закрывался при первом показе.
            popup.ShouldUseOverlayLayer = true;
            popup.IsHitTestVisible = false;
            popup.OverlayDismissEventPassThrough = true;

            if (tip != null)
            {
                tip.IsHitTestVisible = false;
            }

            if (popup.PlacementTarget is Control target)
            {
                popup.OverlayInputPassThroughElement = target;
                ApplyToolTipOffsets(popup, target);
            }

            popup.Opened -= OnPopupOpened;
            popup.Opened += OnPopupOpened;
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
            var popup = element as Popup
                        ?? element.Parent as Popup
                        ?? element.FindAncestorOfType<Popup>()
                        ?? (element as Visual)?.FindDescendantOfType<Popup>();

            if (popup == null)
            {
                element.Loaded += (s, ev) => ApplyOffset(element);
                return;
            }

            popup.Opened -= OnPopupOpened;
            popup.Opened += OnPopupOpened;

            ApplyPopupOffset(element, popup);
        }

        private static void OnPopupOpened(object? sender, System.EventArgs e)
        {
            if (sender is Popup popup)
            {
                if (popup.Child is ToolTip tip)
                {
                    ConfigureToolTipPopup(popup, tip);
                }
                var child = popup.Child as Control ?? popup;
                ApplyPopupOffset(child, popup);
            }
        }

        private static void ApplyPopupOffset(Control element, Popup popup)
        {
            popup.ShouldUseOverlayLayer = true;

            if (element is ToolTip tip)
            {
                ConfigureToolTipPopup(popup, tip);
                return;
            }

            double? hOffset = null;
            double? vOffset = null;

            if (element.IsSet(HorizontalOffsetProperty))
                hOffset = GetHorizontalOffset(element);
            else if (popup.IsSet(HorizontalOffsetProperty))
                hOffset = GetHorizontalOffset(popup);
            else if (popup.PlacementTarget is Control target && target.IsSet(HorizontalOffsetProperty))
                hOffset = GetHorizontalOffset(target);
            else if (popup.FindAncestorOfType<Control>() is Control ancestor && ancestor.IsSet(HorizontalOffsetProperty))
                hOffset = GetHorizontalOffset(ancestor);
            else
            {
                var desc = (element as Visual)?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.IsSet(HorizontalOffsetProperty));
                if (desc != null)
                    hOffset = GetHorizontalOffset(desc);
            }

            if (element.IsSet(VerticalOffsetProperty))
                vOffset = GetVerticalOffset(element);
            else if (popup.IsSet(VerticalOffsetProperty))
                vOffset = GetVerticalOffset(popup);
            else if (popup.PlacementTarget is Control target && target.IsSet(VerticalOffsetProperty))
                vOffset = GetVerticalOffset(target);
            else if (popup.FindAncestorOfType<Control>() is Control ancestor && ancestor.IsSet(VerticalOffsetProperty))
                vOffset = GetVerticalOffset(ancestor);
            else
            {
                var desc = (element as Visual)?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.IsSet(VerticalOffsetProperty));
                if (desc != null)
                    vOffset = GetVerticalOffset(desc);
            }

            // Автоматическая компенсация смещения теней с учетом Placement:
            if (!hOffset.HasValue)
            {
                switch (popup.Placement)
                {
                    case PlacementMode.BottomEdgeAlignedLeft:
                    case PlacementMode.TopEdgeAlignedLeft:
                        hOffset = -12;
                        break;
                    case PlacementMode.BottomEdgeAlignedRight:
                    case PlacementMode.TopEdgeAlignedRight:
                        hOffset = 12;
                        break;
                    case PlacementMode.Bottom:
                    case PlacementMode.Top:
                    case PlacementMode.Center:
                    default:
                        hOffset = 0;
                        break;
                }
            }
            else if (hOffset.Value == -12 && (popup.Placement == PlacementMode.Bottom || popup.Placement == PlacementMode.Top || popup.Placement == PlacementMode.Center))
            {
                // Если пришло унаследованное -12, но выравнивание центрированное — сбрасываем в 0 для точного центрирования
                hOffset = 0;
            }
            else if (hOffset.Value == -12 && (popup.Placement == PlacementMode.BottomEdgeAlignedRight || popup.Placement == PlacementMode.TopEdgeAlignedRight))
            {
                // Если выравнивание по правому краю, компенсация тени должна быть +12, а не -12
                hOffset = 12;
            }

            if (!vOffset.HasValue)
            {
                switch (popup.Placement)
                {
                    case PlacementMode.Top:
                    case PlacementMode.TopEdgeAlignedLeft:
                    case PlacementMode.TopEdgeAlignedRight:
                        vOffset = 12;
                        break;
                    case PlacementMode.Bottom:
                    case PlacementMode.BottomEdgeAlignedLeft:
                    case PlacementMode.BottomEdgeAlignedRight:
                    default:
                        vOffset = -12;
                        break;
                }
            }

            if (hOffset.HasValue)
            {
                popup.HorizontalOffset = hOffset.Value;
            }

            if (vOffset.HasValue)
            {
                popup.VerticalOffset = vOffset.Value;
            }
        }

        private static void ApplyToolTipOffsets(Popup popup, Control target)
        {
            if (target.IsSet(ToolTip.VerticalOffsetProperty))
            {
                popup.VerticalOffset = ToolTip.GetVerticalOffset(target);
            }
            else
            {
                PlacementMode placement = PlacementMode.Bottom;

                if (target.IsSet(ToolTip.PlacementProperty))
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

            if (target.IsSet(ToolTip.HorizontalOffsetProperty))
            {
                popup.HorizontalOffset = ToolTip.GetHorizontalOffset(target);
            }
            else
            {
                PlacementMode placement = PlacementMode.Bottom;

                if (target.IsSet(ToolTip.PlacementProperty))
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
        }
    }
}