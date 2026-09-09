using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Quartz.UI.ViewModels;

namespace Quartz.UI.Tools;

public class LayoutGrid : Grid
{
    public static readonly StyledProperty<SplitContainerVM?> SourceProperty =
        AvaloniaProperty.Register<LayoutGrid, SplitContainerVM?>(nameof(Source));

    private readonly Dictionary<AvaloniaObject, IDisposable> _definitionSubscriptions = new();

    public SplitContainerVM? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            var oldVM = change.OldValue as SplitContainerVM;
            var newVM = change.NewValue as SplitContainerVM;

            if (oldVM != null) oldVM.Children.CollectionChanged -= OnChildrenChanged;
            if (newVM != null) newVM.Children.CollectionChanged += OnChildrenChanged;

            Rebuild();
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        UnsubscribeDefinitions();

        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();

        if (Source == null || Source.Children.Count == 0) return;

        bool isH = Source.IsHorizontal;

        for (int i = 0; i < Source.Children.Count; i++)
        {
            var childVM = Source.Children[i];

            if (isH)
                ColumnDefinitions.Add(new ColumnDefinition(GridLength.Parse(childVM.Size)));
            else
                RowDefinitions.Add(new RowDefinition(GridLength.Parse(childVM.Size)));

            var contentPresenter = new ContentControl
            {
                Content = childVM
            };

            if (isH) Grid.SetColumn(contentPresenter, i);
            else Grid.SetRow(contentPresenter, i);

            Children.Add(contentPresenter);
        }

        for (int i = 0; i < Source.Children.Count - 1; i++)
        {
            var splitter = new GridSplitter
            {
                ResizeDirection = isH ? GridResizeDirection.Columns : GridResizeDirection.Rows,
                ZIndex = 100
            };
            splitter.Classes.Add("overlay-splitter");

            splitter.AddHandler(PointerPressedEvent, (s, e) =>
            {
                if (e.GetCurrentPoint(splitter).Properties.IsLeftButtonPressed) splitter.Classes.Add("dragging");
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            splitter.AddHandler(PointerReleasedEvent, (s, e) => splitter.Classes.Remove("dragging"),
                RoutingStrategies.Bubble, handledEventsToo: true);
            splitter.AddHandler(PointerCaptureLostEvent, (s, e) => splitter.Classes.Remove("dragging"),
                RoutingStrategies.Bubble, handledEventsToo: true);

            if (isH)
            {
                splitter.Width = 4;
                splitter.MinWidth = 4;
                splitter.HorizontalAlignment = HorizontalAlignment.Right;
                splitter.VerticalAlignment = VerticalAlignment.Stretch;
                splitter.Margin = new Thickness(0, 0, -3, 0);

                var leftDef = ColumnDefinitions[i];
                var leftVM = Source.Children[i];
                TrackDefinition(leftDef, () => leftVM.Size = leftDef.Width.ToString());

                var rightDef = ColumnDefinitions[i + 1];
                var rightVM = Source.Children[i + 1];
                TrackDefinition(rightDef, () => rightVM.Size = rightDef.Width.ToString());

                Grid.SetColumn(splitter, i);
            }
            else
            {
                splitter.Height = 4;
                splitter.MinHeight = 4;
                splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                splitter.VerticalAlignment = VerticalAlignment.Bottom;
                splitter.Margin = new Thickness(0, 0, 0, -3);

                var topDef = RowDefinitions[i];
                var topVM = Source.Children[i];
                TrackDefinition(topDef, () => topVM.Size = topDef.Height.ToString());

                var bottomDef = RowDefinitions[i + 1];
                var bottomVM = Source.Children[i + 1];
                TrackDefinition(bottomDef, () => bottomVM.Size = bottomDef.Height.ToString());

                Grid.SetRow(splitter, i);
            }

            Children.Add(splitter);
        }
    }

    private void TrackDefinition(AvaloniaObject definition, Action update)
    {
        UntrackDefinition(definition);

        IDisposable subscription;
        if (definition is ColumnDefinition columnDefinition)
        {
            subscription = columnDefinition.GetObservable(ColumnDefinition.WidthProperty)
                .Subscribe(_ => update());
        }
        else if (definition is RowDefinition rowDefinition)
        {
            subscription = rowDefinition.GetObservable(RowDefinition.HeightProperty)
                .Subscribe(_ => update());
        }
        else
        {
            return;
        }

        _definitionSubscriptions[definition] = subscription;
    }

    private void UntrackDefinition(AvaloniaObject definition)
    {
        if (!_definitionSubscriptions.Remove(definition, out var subscription))
            return;

        subscription.Dispose();
    }

    private void UnsubscribeDefinitions()
    {
        foreach (var subscription in _definitionSubscriptions.Values.ToArray())
        {
            subscription.Dispose();
        }

        _definitionSubscriptions.Clear();
    }
}