using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
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
    private double _savedBottomPanelHeight = 220;
    private double _savedSidebarWidth = 240;
    private MainVM? _subscribedMainVm;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        VerticalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(VerticalSplitter).Properties.IsLeftButtonPressed) VerticalSplitter.Classes.Add("dragging");
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerReleasedEvent, (s, e) =>
        {
            VerticalSplitter.Classes.Remove("dragging");
            SaveCurrentSidebarWidth();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) =>
        {
            VerticalSplitter.Classes.Remove("dragging");
            SaveCurrentSidebarWidth();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        
        HorizontalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(HorizontalSplitter).Properties.IsLeftButtonPressed) HorizontalSplitter.Classes.Add("dragging");
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerReleasedEvent, (s, e) =>
        {
            HorizontalSplitter.Classes.Remove("dragging");
            SaveCurrentBottomPanelHeight();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) =>
        {
            HorizontalSplitter.Classes.Remove("dragging");
            SaveCurrentBottomPanelHeight();
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        ErrorsTreeView.AddHandler(PointerPressedEvent, OnErrorsTreeViewPointerPressed, RoutingStrategies.Tunnel);
        ErrorsTreeView.AddHandler(DoubleTappedEvent, OnErrorsTreeViewDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        ErrorsTreeView.AddHandler(KeyDownEvent, OnErrorsTreeViewKeyDown, RoutingStrategies.Bubble);

        SidebarTreeView.AddHandler(PointerPressedEvent, OnSidebarTreeViewPointerPressed, RoutingStrategies.Tunnel);
        SidebarTreeView.AddHandler(PointerReleasedEvent, OnSidebarTreeViewPointerReleased, RoutingStrategies.Tunnel);
        SidebarTreeView.AddHandler(DoubleTappedEvent, OnSidebarTreeViewDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        SidebarTreeView.AddHandler(KeyDownEvent, OnSidebarTreeViewKeyDown, RoutingStrategies.Bubble);

        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.PointerUpdateKind == PointerUpdateKind.XButton1Pressed || (props.IsXButton1Pressed && props.PointerUpdateKind == PointerUpdateKind.Other))
        {
            if (DataContext is MainVM mainVm && mainVm.CanNavigateBack)
            {
                mainVm.NavigateBack();
            }
            e.Handled = true;
        }
        else if (props.PointerUpdateKind == PointerUpdateKind.XButton2Pressed || (props.IsXButton2Pressed && props.PointerUpdateKind == PointerUpdateKind.Other))
        {
            if (DataContext is MainVM mainVm && mainVm.CanNavigateForward)
            {
                mainVm.NavigateForward();
            }
            e.Handled = true;
        }
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.PointerUpdateKind is PointerUpdateKind.XButton1Released or PointerUpdateKind.XButton2Released)
        {
            e.Handled = true;
        }
    }

    private void OnSidebarTreeViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(SidebarTreeView);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (e.Source is not Avalonia.Visual visualSource)
            return;

        if (visualSource.FindAncestorOfType<ToggleButton>() != null)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (treeViewItem?.DataContext is FileStructVM fileStruct && DataContext is MainVM mainVm)
        {
            mainVm.TappedFile = fileStruct;
            var pos = e.GetPosition(null);
            mainVm.DragStartX = pos.X;
            mainVm.DragStartY = pos.Y;

            if (fileStruct is FileVM fileVm)
            {
                mainVm.OpenFileTab(fileVm);
            }
            else if (fileStruct is DirectoryVM dirVm)
            {
                dirVm.IsExpanded = !dirVm.IsExpanded;
            }
        }
    }

    private void OnSidebarTreeViewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainVM mainVm)
            return;

        if (mainVm.DraggedFile != null)
        {
            if (e.Source is Avalonia.Visual visualSource)
            {
                var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>(includeSelf: true);
                if (treeViewItem?.DataContext is FileStructVM target)
                {
                    target.TapEndCommand.Execute(null);
                    return;
                }
            }
        }

        mainVm.ResetDragStatus();
    }

    private void OnSidebarTreeViewDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Avalonia.Visual visualSource || visualSource.FindAncestorOfType<ToggleButton>() != null)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (treeViewItem?.DataContext is FileVM fileVm && DataContext is MainVM mainVm)
        {
            mainVm.OpenFileTab(fileVm);
            e.Handled = true;
        }
        else if (treeViewItem?.DataContext is DirectoryVM dirVm)
        {
            dirVm.IsExpanded = !dirVm.IsExpanded;
            e.Handled = true;
        }
    }

    private void OnSidebarTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SidebarTreeView.SelectedItem is FileVM fileVm && DataContext is MainVM mainVm)
        {
            mainVm.OpenFileTab(fileVm);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SidebarTreeView.SelectedItem is DirectoryVM dirVm)
        {
            dirVm.IsExpanded = !dirVm.IsExpanded;
            e.Handled = true;
        }
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

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (treeViewItem == null)
            return;

        HandleErrorNavigation(treeViewItem.DataContext);
    }

    private void OnErrorsTreeViewDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Avalonia.Visual visualSource || visualSource.FindAncestorOfType<ToggleButton>() != null)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>(includeSelf: true);
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
            if (mainVm.IsBottomPanelMaximized)
            {
                mainVm.IsBottomPanelMaximized = false;
            }
            mainVm.OpenFileTab(fileVm);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedMainVm != null)
        {
            _subscribedMainVm.PropertyChanged -= OnMainVmPropertyChanged;
        }

        _subscribedMainVm = DataContext as MainVM;

        if (_subscribedMainVm != null)
        {
            _subscribedMainVm.PropertyChanged += OnMainVmPropertyChanged;
            UpdateBottomPanelLayout(_subscribedMainVm);
            UpdateSidebarLayout(_subscribedMainVm);
        }
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainVM.IsBottomPanelVisible) or nameof(MainVM.IsBottomPanelMaximized))
        {
            if (_subscribedMainVm != null)
            {
                UpdateBottomPanelLayout(_subscribedMainVm);
            }
        }
        else if (e.PropertyName == nameof(MainVM.IsSidebarVisible))
        {
            if (_subscribedMainVm != null)
            {
                UpdateSidebarLayout(_subscribedMainVm);
            }
        }
    }

    private void UpdateBottomPanelLayout(MainVM vm)
    {
        if (MainGrid.RowDefinitions.Count < 3)
            return;

        if (!vm.IsBottomPanelVisible)
        {
            SaveCurrentBottomPanelHeight();
            MainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            MainGrid.RowDefinitions[2].Height = new GridLength(0);
        }
        else if (vm.IsBottomPanelMaximized)
        {
            SaveCurrentBottomPanelHeight();
            MainGrid.RowDefinitions[1].Height = new GridLength(0);
            MainGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            double targetHeight = _savedBottomPanelHeight > 30 ? _savedBottomPanelHeight : 220;
            MainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            MainGrid.RowDefinitions[2].Height = new GridLength(targetHeight, GridUnitType.Pixel);
        }
    }

    private void SaveCurrentBottomPanelHeight()
    {
        if (_subscribedMainVm != null && _subscribedMainVm.IsBottomPanelVisible && !_subscribedMainVm.IsBottomPanelMaximized)
        {
            if (MainGrid.RowDefinitions.Count >= 3)
            {
                var row2Height = MainGrid.RowDefinitions[2].Height;
                if (row2Height.IsAbsolute && row2Height.Value > 30)
                {
                    _savedBottomPanelHeight = row2Height.Value;
                }
            }
        }
    }

    private void UpdateSidebarLayout(MainVM vm)
    {
        if (MainGrid.ColumnDefinitions.Count < 2)
            return;

        if (!vm.IsSidebarVisible)
        {
            SaveCurrentSidebarWidth();
            MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }
        else
        {
            double targetWidth = _savedSidebarWidth > 30 ? _savedSidebarWidth : 240;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(targetWidth, GridUnitType.Pixel);
        }
    }

    private void SaveCurrentSidebarWidth()
    {
        if (_subscribedMainVm != null && _subscribedMainVm.IsSidebarVisible)
        {
            if (MainGrid.ColumnDefinitions.Count >= 2)
            {
                var col1 = MainGrid.ColumnDefinitions[1];
                if (col1.Width.IsAbsolute && col1.Width.Value > 30)
                {
                    _savedSidebarWidth = col1.Width.Value;
                }
                else if (SidebarGrid.Bounds.Width > 30)
                {
                    _savedSidebarWidth = SidebarGrid.Bounds.Width;
                }
            }
        }
    }
}