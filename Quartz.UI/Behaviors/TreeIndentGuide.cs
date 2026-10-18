using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Quartz.UI.Behaviors;

public class TreeIndentGuide : Control
{
    private Pen? _linePen;
    private IDisposable? _expandedSub;

    public TreeIndentGuide()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePen();

        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.ActualThemeVariantChanged += OnThemeChanged;
        }

        var treeViewItem = this.FindAncestorOfType<TreeViewItem>();
        if (treeViewItem != null)
        {
            _expandedSub = treeViewItem.GetObservable(TreeViewItem.IsExpandedProperty)
                .Subscribe(_ => InvalidateVisual());
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.ActualThemeVariantChanged -= OnThemeChanged;
        }
        _expandedSub?.Dispose();
        _expandedSub = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdatePen();
        InvalidateVisual();
    }

    private void UpdatePen()
    {
        var targetTheme = Avalonia.Application.Current?.ActualThemeVariant ?? Avalonia.Styling.ThemeVariant.Dark;
        if (this.TryFindResource("BorderColor60", targetTheme, out var res))
        {
            if (res is IBrush brush)
                _linePen = new Pen(brush, 1.0);
            else if (res is Color color)
                _linePen = new Pen(new SolidColorBrush(color), 1.0);
        }
        else if (this.TryFindResource("BorderColor", targetTheme, out var res2))
        {
            if (res2 is IBrush brush)
                _linePen = new Pen(brush, 1.0);
            else if (res2 is Color color)
                _linePen = new Pen(new SolidColorBrush(color), 1.0);
        }
        else
        {
            _linePen = new Pen(new SolidColorBrush(Color.FromArgb(100, 128, 128, 128)), 1.0);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var treeViewItem = this.FindAncestorOfType<TreeViewItem>();
        if (treeViewItem == null) return;

        int level = treeViewItem.Level;
        if (level <= 0) return; // Top-level items have no ancestor guides, and folder chevron itself has no line

        if (_linePen == null)
            UpdatePen();

        if (_linePen == null) return;

        double indent = 24.0;
        if (this.TryFindResource("TreeViewItemIndent", out var indentObj) && indentObj is double d)
        {
            indent = d;
        }

        // Center of chevron in column 0 (default width is 18px, center is 9.0)
        double chevronOffset = 9.0;
        var chevronContainer = (treeViewItem as Visual).GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => c.Name == "PART_ExpandCollapseChevronContainer");

        if (chevronContainer != null && chevronContainer.Bounds.Width > 0)
        {
            var p = chevronContainer.TranslatePoint(new Point(chevronContainer.Bounds.Width / 2.0, 0), this);
            if (p.HasValue)
            {
                chevronOffset = p.Value.X - level * indent;
            }
            else
            {
                chevronOffset = chevronContainer.Bounds.X + chevronContainer.Bounds.Width / 2.0;
            }
        }

        double h = Bounds.Height;
        if (h <= 0) return;

        // Ancestor guide lines (0 .. level - 1)
        // Extend lines slightly above and below (-3 to h + 3) to seamlessly bridge micro-gaps between TreeViewItems
        for (int i = 0; i < level; i++)
        {
            double lineX = i * indent + chevronOffset;
            double snappedX = Math.Floor(lineX) + 0.5;
            context.DrawLine(_linePen, new Point(snappedX, -3), new Point(snappedX, h + 3));
        }
    }
}

public class TreeIndentGuideAssist
{
    public static readonly AttachedProperty<bool> ShowGuidesProperty =
        AvaloniaProperty.RegisterAttached<TreeIndentGuideAssist, Border, bool>("ShowGuides");

    public static bool GetShowGuides(Border element) => element.GetValue(ShowGuidesProperty);
    public static void SetShowGuides(Border element, bool value) => element.SetValue(ShowGuidesProperty, value);

    static TreeIndentGuideAssist()
    {
        ShowGuidesProperty.Changed.AddClassHandler<Border>(OnShowGuidesChanged);
    }

    private static void OnShowGuidesChanged(Border border, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            void Apply()
            {
                if (border.Child is Panel panel && panel.Children.Any(c => c is TreeIndentGuide))
                    return;

                if (border.Child != null && border.Child is not TreeIndentGuide)
                {
                    var content = border.Child;
                    border.Child = null;
                    border.ClipToBounds = false;
                    var wrapper = new Panel { ClipToBounds = false };
                    wrapper.Children.Add(new TreeIndentGuide());
                    wrapper.Children.Add(content);
                    border.Child = wrapper;
                }
            }

            if (border.Child != null)
            {
                Apply();
            }
            else
            {
                border.Loaded += (s, ev) => Apply();
            }

            border.PropertyChanged += (s, ev) =>
            {
                if (ev.Property == Border.ChildProperty && border.Child != null && border.Child is not Panel)
                {
                    Apply();
                }
            };
        }
    }
}
