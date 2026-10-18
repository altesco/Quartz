using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using Quartz.UI.ViewModels;
using ShadUI;
using Window = Avalonia.Controls.Window;

namespace Quartz.UI.Behaviors;

public class BreadcrumbTreeBehavior : Behavior<Control>
{
    public Thickness? PopupPadding { get; set; }

    private Point _pressedPoint;
    private bool _pressedOnChevron;
    private Window? _window;
    private IDisposable? _windowBoundsSub;
    private IDisposable? _dropDownOpenSub;
    private Border? _styledBorder;
    private TreeView? _hookedTreeView;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;
        AssociatedObject.DetachedFromVisualTree += OnDetachedFromVisualTree;

        if (AssociatedObject is SimpleDropdown dropdown)
        {
            _dropDownOpenSub = dropdown.GetObservable(SimpleDropdown.IsDropDownOpenProperty)
                .Subscribe(OnIsDropDownOpenChanged);
        }

        if (AssociatedObject.IsAttachedToVisualTree())
        {
            SetupWindowSubscription();
            if (AssociatedObject is SimpleDropdown dd && dd.IsDropDownOpen)
            {
                OnIsDropDownOpenChanged(true);
            }
            else if (AssociatedObject is TreeView)
            {
                HookTreeView();
                UpdateConstraints();
                ApplyPopupStyling();
            }
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
            AssociatedObject.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }

        _dropDownOpenSub?.Dispose();
        _dropDownOpenSub = null;

        TeardownWindowSubscription();
        UnhookTreeView();
        CleanupStyledBorder();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SetupWindowSubscription();
        if (AssociatedObject is SimpleDropdown dd && dd.IsDropDownOpen)
        {
            OnIsDropDownOpenChanged(true);
        }
        else if (AssociatedObject is TreeView)
        {
            HookTreeView();
            UpdateConstraints();
            ApplyPopupStyling();
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        TeardownWindowSubscription();
        UnhookTreeView();
        CleanupStyledBorder();
    }

    private void OnIsDropDownOpenChanged(bool isOpen)
    {
        if (isOpen)
        {
            UpdateConstraints();
            ApplyPopupStyling();

            Dispatcher.UIThread.Post(() =>
            {
                UpdateConstraints();
                ApplyPopupStyling();
                HookTreeView();
            }, DispatcherPriority.Loaded);
        }
        else
        {
            UnhookTreeView();
        }
    }

    private void SetupWindowSubscription()
    {
        TeardownWindowSubscription();
        _window = FindWindow();
        if (_window != null)
        {
            _windowBoundsSub = _window.GetObservable(Visual.BoundsProperty).Subscribe(OnWindowBoundsChanged);
            _window.ActualThemeVariantChanged += OnThemeChanged;
            UpdateConstraints();
        }

        var dropdown = GetDropdown();
        if (dropdown != null)
        {
            dropdown.ActualThemeVariantChanged += OnThemeChanged;
        }
    }

    private void TeardownWindowSubscription()
    {
        if (_window != null)
        {
            _window.ActualThemeVariantChanged -= OnThemeChanged;
        }

        var dropdown = GetDropdown();
        if (dropdown != null)
        {
            dropdown.ActualThemeVariantChanged -= OnThemeChanged;
        }

        _windowBoundsSub?.Dispose();
        _windowBoundsSub = null;
        _window = null;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        var targetTheme = GetCurrentThemeVariant();
        var border = FindPopupBorder();
        if (border != null)
        {
            if (TopLevel.GetTopLevel(border) is TopLevel tl)
            {
                tl.RequestedThemeVariant = targetTheme;
            }
            UpdateBorderColors(border);
        }
    }

    private void OnWindowBoundsChanged(Rect bounds)
    {
        var dropdown = GetDropdown();
        if (dropdown != null && dropdown.IsDropDownOpen)
        {
            UpdateConstraints();
        }
    }

    private Window? FindWindow()
    {
        if (AssociatedObject != null)
        {
            var topLevel = TopLevel.GetTopLevel(AssociatedObject);
            if (topLevel is Window w1)
                return w1;
            if (topLevel is PopupRoot pr && pr.Parent is Visual parentVisual && TopLevel.GetTopLevel(parentVisual) is Window w2)
                return w2;
        }

        var dropdown = GetDropdown();
        if (dropdown != null && TopLevel.GetTopLevel(dropdown) is Window w3)
            return w3;

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
    }

    private SimpleDropdown? GetDropdown()
    {
        if (AssociatedObject is SimpleDropdown dd)
            return dd;

        if (AssociatedObject != null)
        {
            var popupRoot = TopLevel.GetTopLevel(AssociatedObject) as PopupRoot;
            var popup = popupRoot?.Parent as Popup;
            var parentDd = (popup?.Parent as Visual)?.TemplatedParent as SimpleDropdown;
            parentDd ??= popup?.PlacementTarget as SimpleDropdown ?? popup?.PlacementTarget?.FindAncestorOfType<SimpleDropdown>();
            parentDd ??= AssociatedObject.FindLogicalAncestorOfType<SimpleDropdown>();
            if (parentDd != null) return parentDd;
        }

        return null;
    }

    private void CleanupStyledBorder()
    {
        if (_styledBorder != null)
        {
            _styledBorder.ActualThemeVariantChanged -= OnBorderThemeChanged;
            _styledBorder = null;
        }
    }

    private ThemeVariant GetCurrentThemeVariant()
    {
        var dropdown = GetDropdown();
        if (dropdown != null && dropdown.ActualThemeVariant != ThemeVariant.Default)
            return dropdown.ActualThemeVariant;

        var window = _window ?? FindWindow();
        if (window != null && window.ActualThemeVariant != ThemeVariant.Default)
            return window.ActualThemeVariant;

        if (Avalonia.Application.Current != null && Avalonia.Application.Current.ActualThemeVariant != ThemeVariant.Default)
            return Avalonia.Application.Current.ActualThemeVariant;

        return ThemeVariant.Dark;
    }

    private void ApplyPopupStyling()
    {
        var targetTheme = GetCurrentThemeVariant();

        var popup = FindPopup();
        if (popup != null)
        {
            popup.HorizontalOffset = -12;
            popup.VerticalOffset = -12;
            popup.Placement = PlacementMode.BottomEdgeAlignedLeft;
            popup.PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideY | PopupPositionerConstraintAdjustment.FlipY;
        }

        var border = FindPopupBorder();
        if (border != null)
        {
            if (TopLevel.GetTopLevel(border) is TopLevel tl)
            {
                tl.RequestedThemeVariant = targetTheme;
            }

            if (_styledBorder != null && _styledBorder != border)
            {
                _styledBorder.ActualThemeVariantChanged -= OnBorderThemeChanged;
            }
            _styledBorder = border;

            // Box shadow matching context menu: 0 6 12 0 #40000000
            border.BoxShadow = BoxShadows.Parse("0 6 12 0 #40000000");

            // Margin 12px gives room for the 12px blur shadow inside PopupRoot without clipping
            border.Margin = new Thickness(12);

            // Border styling
            border.BorderThickness = new Thickness(1);
            if (border.TryFindResource("SmCornerRadius", out var cr) && cr is CornerRadius radius)
            {
                border.CornerRadius = radius;
            }
            else
            {
                border.CornerRadius = new CornerRadius(4);
            }

            // Only override padding if explicitly configured in behavior; otherwise respect style/XAML
            if (PopupPadding.HasValue)
            {
                border.Padding = PopupPadding.Value;
            }

            // Zero out any template margins on intermediate containers
            if (border.Child is StackPanel sp)
            {
                sp.Margin = new Thickness(0);
                foreach (var child in sp.Children)
                {
                    if (child is ScrollViewer sv)
                    {
                        sv.Margin = new Thickness(0);
                    }
                }
            }

            UpdateBorderColors(border);

            border.ActualThemeVariantChanged -= OnBorderThemeChanged;
            border.ActualThemeVariantChanged += OnBorderThemeChanged;
        }

        var dropdown = GetDropdown();
        if (dropdown != null)
        {
            dropdown.PopupHorizontalOffset = -12;
            dropdown.PopupVerticalOffset = -12;
            dropdown.PopupPlacement = PlacementMode.BottomEdgeAlignedLeft;
        }
    }

    private void OnBorderThemeChanged(object? sender, EventArgs e)
    {
        if (sender is Border border)
        {
            UpdateBorderColors(border);
        }
    }

    private void UpdateBorderColors(Border border)
    {
        var targetTheme = GetCurrentThemeVariant();
        if (TopLevel.GetTopLevel(border) is TopLevel tl)
        {
            tl.RequestedThemeVariant = targetTheme;
        }

        var borderBrush = ResolveBrush(border, "BorderColor60", targetTheme);
        if (borderBrush != null)
        {
            border.BorderBrush = borderBrush;
        }

        var cardBgBrush = ResolveBrush(border, "CardBackgroundColor", targetTheme);
        if (cardBgBrush != null)
        {
            border.Background = cardBgBrush;
        }
    }

    private static IBrush? ResolveBrush(IResourceHost host, string key, ThemeVariant targetTheme)
    {
        if (host is IResourceNode node && node.TryGetResource(key, targetTheme, out var resource))
        {
            if (resource is IBrush brush)
                return brush;
            if (resource is Color color)
                return new SolidColorBrush(color);
        }

        if (Avalonia.Application.Current is IResourceNode appNode && appNode.TryGetResource(key, targetTheme, out var appRes))
        {
            if (appRes is IBrush appBrush)
                return appBrush;
            if (appRes is Color appColor)
                return new SolidColorBrush(appColor);
        }

        if (host.TryFindResource(key, out var fallback))
        {
            if (fallback is IBrush b) return b;
            if (fallback is Color c) return new SolidColorBrush(c);
        }

        if (Avalonia.Application.Current != null && Avalonia.Application.Current.TryFindResource(key, out var globalFallback))
        {
            if (globalFallback is IBrush gb) return gb;
            if (globalFallback is Color gc) return new SolidColorBrush(gc);
        }

        return null;
    }

    private void UpdateConstraints()
    {
        var dropdown = GetDropdown();
        if (dropdown == null) return;

        var window = _window ?? FindWindow();
        if (window == null) return;

        double windowWidth = window.Bounds.Width;
        double windowHeight = window.Bounds.Height;
        if (windowWidth <= 0 || windowHeight <= 0) return;

        var targetPoint = dropdown.TranslatePoint(new Point(0, 0), window);
        if (!targetPoint.HasValue) return;

        double targetLeft = Math.Max(0, targetPoint.Value.X);
        double targetBottom = Math.Max(0, targetPoint.Value.Y + dropdown.Bounds.Height);

        // Calculate available width to the right: distance from trigger left edge to the window right margin (60px).
        // This ensures the popup stays anchored at the bottom-left of the trigger and does not shift left.
        double maxAllowedWidth = Math.Max(100, windowWidth - targetLeft - 60);
        double menuWidth = Math.Min(450, maxAllowedWidth);

        var border = FindPopupBorder();
        double hPad = border != null ? border.Padding.Left + border.Padding.Right : 8;
        double vPad = border != null ? border.Padding.Top + border.Padding.Bottom : 8;
        double bThickX = border != null ? border.BorderThickness.Left + border.BorderThickness.Right : 2;
        double bThickY = border != null ? border.BorderThickness.Top + border.BorderThickness.Bottom : 2;
        double contentWidth = Math.Max(50, menuWidth - hPad - bThickX);

        // Height constraint: 60px margin from bottom of the window, capped at 450
        double maxAllowedHeight = targetBottom > 0
            ? Math.Max(100, windowHeight - targetBottom - 60)
            : Math.Max(100, windowHeight - 120);
        double menuHeight = Math.Min(450, maxAllowedHeight);
        double contentHeight = Math.Max(50, menuHeight - vPad - bThickY);

        // Set popup width and placement properties on SimpleDropdown
        dropdown.PopupWidth = menuWidth + 24; // 24px = 12px margin on each side for box shadow
        dropdown.PopupPlacement = PlacementMode.BottomEdgeAlignedLeft;
        dropdown.PopupHorizontalOffset = -12;
        dropdown.PopupVerticalOffset = -12;

        var popup = FindPopup();
        if (popup != null)
        {
            popup.Width = menuWidth + 24;
            popup.Placement = PlacementMode.BottomEdgeAlignedLeft;
            popup.HorizontalOffset = -12;
            popup.VerticalOffset = -12;
            popup.PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideY | PopupPositionerConstraintAdjustment.FlipY;
        }

        border ??= FindPopupBorder();
        if (border != null)
        {
            border.Width = menuWidth;
            border.MaxWidth = menuWidth;
            border.MaxHeight = menuHeight;
        }

        var grid = FindInnerGrid();
        if (grid != null)
        {
            grid.Width = contentWidth;
            grid.MaxWidth = contentWidth;
            grid.MaxHeight = contentHeight;
        }

        var scrollViewer = FindInnerScrollViewer();
        if (scrollViewer != null)
        {
            scrollViewer.MaxHeight = contentHeight;
        }
    }

    private Popup? FindPopup()
    {
        var dropdown = GetDropdown();
        if (dropdown != null)
        {
            var popup = dropdown.FindDescendantOfType<Popup>();
            if (popup != null) return popup;
        }

        if (AssociatedObject != null)
        {
            var popupRoot = TopLevel.GetTopLevel(AssociatedObject) as PopupRoot;
            if (popupRoot?.Parent is Popup p) return p;
            return AssociatedObject.FindAncestorOfType<Popup>();
        }

        return null;
    }

    private Border? FindPopupBorder()
    {
        var popup = FindPopup();
        if (popup != null)
        {
            if (popup.Child is LayoutTransformControl ltc && ltc.Child is Border b)
                return b;
            var border = popup.FindDescendantOfType<Border>();
            if (border != null) return border;
        }

        if (AssociatedObject != null)
        {
            var popupRoot = TopLevel.GetTopLevel(AssociatedObject) as PopupRoot;
            if (popupRoot != null)
            {
                var b = popupRoot.FindDescendantOfType<Border>();
                if (b != null) return b;
            }
        }

        return null;
    }

    private Grid? FindInnerGrid()
    {
        var dropdown = GetDropdown();
        if (dropdown != null)
        {
            foreach (var item in dropdown.Items)
            {
                if (item is Grid g) return g;
                if (item is Visual v && v.FindDescendantOfType<Grid>() is { } dg) return dg;
            }
        }

        if (AssociatedObject != null)
        {
            return AssociatedObject.FindAncestorOfType<Grid>() ?? AssociatedObject.FindDescendantOfType<Grid>();
        }

        return null;
    }

    private ScrollViewer? FindInnerScrollViewer()
    {
        var grid = FindInnerGrid();
        if (grid != null)
        {
            return grid.FindDescendantOfType<ScrollViewer>();
        }
        return null;
    }

    private TreeView? FindTreeView()
    {
        if (AssociatedObject is TreeView tv) return tv;

        var grid = FindInnerGrid();
        if (grid != null)
        {
            var treeView = grid.FindDescendantOfType<TreeView>();
            if (treeView != null) return treeView;
        }

        var popup = FindPopup();
        if (popup != null)
        {
            return popup.FindDescendantOfType<TreeView>();
        }

        return null;
    }

    private void HookTreeView()
    {
        var treeView = FindTreeView();
        if (treeView != null && treeView.ItemContainerTheme == null)
        {
            if (treeView.TryFindResource("QuartzTreeViewItemTheme", out var themeObj) && themeObj is ControlTheme ct)
            {
                treeView.ItemContainerTheme = ct;
            }
            else if (Avalonia.Application.Current != null && Avalonia.Application.Current.TryFindResource("QuartzTreeViewItemTheme", out var appThemeObj) && appThemeObj is ControlTheme appCt)
            {
                treeView.ItemContainerTheme = appCt;
            }
        }

        if (treeView == null || treeView == _hookedTreeView) return;

        UnhookTreeView();

        _hookedTreeView = treeView;
        _hookedTreeView.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _hookedTreeView.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        _hookedTreeView.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        _hookedTreeView.AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void UnhookTreeView()
    {
        if (_hookedTreeView != null)
        {
            _hookedTreeView.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            _hookedTreeView.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            _hookedTreeView.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
            _hookedTreeView.RemoveHandler(Button.ClickEvent, OnButtonClick);
            _hookedTreeView = null;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        // ToggleButton is used for expand/collapse chevron in TreeViewItem.
        // SimpleDropdown closes whenever any Button.ClickEvent bubbles up to its Items.
        // Marking this event Handled stops it from reaching SimpleDropdown, keeping the dropdown open!
        if (e.Source is ToggleButton || (e.Source as Visual)?.FindAncestorOfType<ToggleButton>() != null)
        {
            e.Handled = true;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var targetTreeView = _hookedTreeView ?? (AssociatedObject as TreeView);
        if (targetTreeView == null) return;

        var point = e.GetCurrentPoint(targetTreeView);
        if (point.Properties.IsLeftButtonPressed)
        {
            _pressedPoint = e.GetPosition(targetTreeView);
            var visual = e.Source as Visual;
            _pressedOnChevron = visual?.FindAncestorOfType<ToggleButton>() != null;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _pressedOnChevron = false;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        if (_pressedOnChevron)
        {
            _pressedOnChevron = false;
            return;
        }

        var targetTreeView = _hookedTreeView ?? (AssociatedObject as TreeView);
        if (targetTreeView == null) return;

        var releasePoint = e.GetPosition(targetTreeView);
        var diff = releasePoint - _pressedPoint;
        if (Math.Abs(diff.X) > 8 || Math.Abs(diff.Y) > 8)
            return; // Drag/scroll gesture, ignore

        var visual = e.Source as Visual;
        if (visual == null)
            return;

        if (visual.FindAncestorOfType<ToggleButton>() != null)
            return;

        var treeViewItem = visual.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (treeViewItem == null)
            return;

        // Only files open on single click and dismiss the dropdown.
        // Directories expand/collapse on double-click or chevron click (default TreeView behavior).
        if (treeViewItem.DataContext is FileVM fileVm)
        {
            fileVm.OpenFile();

            var dropdown = GetDropdown();
            if (dropdown != null)
            {
                dropdown.SetCurrentValue(SimpleDropdown.IsDropDownOpenProperty, false);
            }

            e.Handled = true;
        }
    }
}
