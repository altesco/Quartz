using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Quartz.Core.Models;
using Quartz.UI.ViewModels;

namespace Quartz.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        VerticalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(VerticalSplitter).Properties.IsLeftButtonPressed) VerticalSplitter.Classes.Add("dragging");
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerReleasedEvent, (s, e) => VerticalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) => VerticalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);
        
        HorizontalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(HorizontalSplitter).Properties.IsLeftButtonPressed) HorizontalSplitter.Classes.Add("dragging");
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerReleasedEvent, (s, e) => HorizontalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) => HorizontalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);

        ErrorsTreeView.AddHandler(PointerPressedEvent, OnErrorsTreeViewPointerPressed, RoutingStrategies.Tunnel);
        ErrorsTreeView.AddHandler(DoubleTappedEvent, OnErrorsTreeViewDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        ErrorsTreeView.AddHandler(KeyDownEvent, OnErrorsTreeViewKeyDown, RoutingStrategies.Bubble);
    }

    private void OnErrorsTreeViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(ErrorsTreeView);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (e.Source is not Avalonia.Visual visualSource)
            return;

        if (visualSource.FindAncestorOfType<ToggleButton>() != null)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>();
        if (treeViewItem == null)
            return;

        HandleErrorNavigation(treeViewItem.DataContext);
    }

    private void OnErrorsTreeViewDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Avalonia.Visual visualSource || visualSource.FindAncestorOfType<ToggleButton>() != null)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>();
        if (treeViewItem == null)
            return;

        HandleErrorNavigation(treeViewItem.DataContext);
    }

    private void OnErrorsTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ErrorsTreeView.SelectedItem != null)
        {
            HandleErrorNavigation(ErrorsTreeView.SelectedItem);
            e.Handled = true;
        }
    }

    private void HandleErrorNavigation(object? dataContext)
    {
        if (DataContext is not MainVM mainVm || dataContext == null)
            return;

        if (dataContext is EditorError error)
        {
            var fileVm = mainVm.ProjectErrors.FirstOrDefault(f => f.Errors.Contains(error));
            if (fileVm != null)
            {
                mainVm.NavigateToError(fileVm, error);
            }
        }
        else if (dataContext is FileVM fileVm)
        {
            mainVm.OpenFileTab(fileVm);
        }
    }
}