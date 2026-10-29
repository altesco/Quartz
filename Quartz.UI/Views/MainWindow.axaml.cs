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
using Quartz.UI.Enums;
using Quartz.UI.ViewModels;

namespace Quartz.UI.Views;

public partial class MainWindow : Window
{
    private const double MinSidebarWidth = 185;
    private const double MinBottomPanelHeight = 75;

    private double _savedBottomPanelHeight = 220;
    private double _savedSidebarWidth = 240;

    private double _preDragSidebarWidth = 240;
    private double _sidebarDragStartCursorX;
    private double _sidebarDragStartWidth;
    private bool _isSidebarCollapsedPreview;

    private double _preDragBottomPanelHeight = 220;
    private double _bottomPanelDragStartCursorY;
    private double _bottomPanelDragStartHeight;
    private bool _isBottomPanelCollapsedPreview;

    private MainVM? _subscribedMainVm;

    public MainWindow()
    {
        InitializeComponent();

        if (MainGrid.ColumnDefinitions.Count >= 2)
        {
            MainGrid.ColumnDefinitions[1].MinWidth = MinSidebarWidth;
        }
        if (MainGrid.RowDefinitions.Count >= 3)
        {
            MainGrid.RowDefinitions[2].MinHeight = MinBottomPanelHeight;
        }

        DataContextChanged += OnDataContextChanged;

        VerticalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(VerticalSplitter).Properties.IsLeftButtonPressed)
            {
                VerticalSplitter.Classes.Add("dragging");
                e.Pointer.Capture(VerticalSplitter);
                _preDragSidebarWidth = MainGrid.ColumnDefinitions.Count >= 2 && MainGrid.ColumnDefinitions[1].Width.IsAbsolute && MainGrid.ColumnDefinitions[1].Width.Value >= MinSidebarWidth
                    ? MainGrid.ColumnDefinitions[1].Width.Value
                    : _savedSidebarWidth;
                _sidebarDragStartWidth = MainGrid.ColumnDefinitions.Count >= 2 && MainGrid.ColumnDefinitions[1].ActualWidth >= MinSidebarWidth
                    ? MainGrid.ColumnDefinitions[1].ActualWidth
                    : _preDragSidebarWidth;
                _sidebarDragStartCursorX = e.GetPosition(MainGrid).X;
                _isSidebarCollapsedPreview = false;
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerMovedEvent, (s, e) =>
        {
            if (VerticalSplitter.Classes.Contains("dragging"))
            {
                OnVerticalSplitterPointerMoved(e);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerReleasedEvent, (s, e) =>
        {
            if (VerticalSplitter.Classes.Contains("dragging"))
            {
                OnVerticalSplitterPointerReleased(e);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) =>
        {
            if (VerticalSplitter.Classes.Contains("dragging"))
            {
                OnVerticalSplitterPointerCaptureLost();
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        
        HorizontalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(HorizontalSplitter).Properties.IsLeftButtonPressed)
            {
                HorizontalSplitter.Classes.Add("dragging");
                e.Pointer.Capture(HorizontalSplitter);
                _preDragBottomPanelHeight = MainGrid.RowDefinitions.Count >= 3 && MainGrid.RowDefinitions[2].Height.IsAbsolute && MainGrid.RowDefinitions[2].Height.Value >= MinBottomPanelHeight
                    ? MainGrid.RowDefinitions[2].Height.Value
                    : _savedBottomPanelHeight;
                _bottomPanelDragStartHeight = MainGrid.RowDefinitions.Count >= 3 && MainGrid.RowDefinitions[2].ActualHeight >= MinBottomPanelHeight
                    ? MainGrid.RowDefinitions[2].ActualHeight
                    : _preDragBottomPanelHeight;
                _bottomPanelDragStartCursorY = e.GetPosition(MainGrid).Y;
                _isBottomPanelCollapsedPreview = false;
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerMovedEvent, (s, e) =>
        {
            if (HorizontalSplitter.Classes.Contains("dragging"))
            {
                OnHorizontalSplitterPointerMoved(e);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerReleasedEvent, (s, e) =>
        {
            if (HorizontalSplitter.Classes.Contains("dragging"))
            {
                OnHorizontalSplitterPointerReleased(e);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) =>
        {
            if (HorizontalSplitter.Classes.Contains("dragging"))
            {
                OnHorizontalSplitterPointerCaptureLost();
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        ErrorsTreeView.AddHandler(PointerPressedEvent, OnErrorsTreeViewPointerPressed, RoutingStrategies.Tunnel);
        ErrorsTreeView.AddHandler(DoubleTappedEvent, OnErrorsTreeViewDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        ErrorsTreeView.AddHandler(KeyDownEvent, OnErrorsTreeViewKeyDown, RoutingStrategies.Bubble);

        SidebarTreeView.AddHandler(PointerPressedEvent, OnSidebarTreeViewPointerPressed, RoutingStrategies.Tunnel);
        SidebarTreeView.AddHandler(PointerReleasedEvent, OnSidebarTreeViewPointerReleased, RoutingStrategies.Tunnel);
        SidebarTreeView.AddHandler(DoubleTappedEvent, OnSidebarTreeViewDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        SidebarTreeView.AddHandler(KeyDownEvent, OnSidebarTreeViewKeyDown, RoutingStrategies.Bubble);

        AddHandler(PointerMovedEvent, (s, e) =>
        {
            if (VerticalSplitter.Classes.Contains("dragging"))
            {
                OnVerticalSplitterPointerMoved(e);
            }
            else if (HorizontalSplitter.Classes.Contains("dragging"))
            {
                OnHorizontalSplitterPointerMoved(e);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

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
        if (VerticalSplitter.Classes.Contains("dragging"))
        {
            OnVerticalSplitterPointerReleased(e);
        }
        else if (HorizontalSplitter.Classes.Contains("dragging"))
        {
            OnHorizontalSplitterPointerReleased(e);
        }

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

        if (dataContext is Problem problem)
        {
            var fileVm = mainVm.ProjectErrors.FirstOrDefault(f => f.Problems.Contains(problem));
            if (fileVm != null)
            {
                mainVm.NavigateToProblem(fileVm, problem);
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

    private void OnVerticalSplitterPointerMoved(PointerEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainVM mainVm || !mainVm.IsSidebarVisible)
            return;

        double deltaX = e.GetPosition(MainGrid).X - _sidebarDragStartCursorX;
        double rawWidth = _sidebarDragStartWidth + deltaX;

        const double collapseThreshold = MinSidebarWidth / 2.0; // 92.5
        if (rawWidth < collapseThreshold)
        {
            if (!_isSidebarCollapsedPreview)
            {
                _isSidebarCollapsedPreview = true;
                MainGrid.ColumnDefinitions[1].MinWidth = 0;
                MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
                SidebarGrid.IsVisible = false;
            }
        }
        else
        {
            _isSidebarCollapsedPreview = false;
            SidebarGrid.IsVisible = true;
            MainGrid.ColumnDefinitions[1].MinWidth = MinSidebarWidth;
            double maxWidth = Math.Max(MinSidebarWidth, MainGrid.Bounds.Width - 100);
            double targetWidth = Math.Clamp(rawWidth, MinSidebarWidth, maxWidth);
            MainGrid.ColumnDefinitions[1].Width = new GridLength(targetWidth, GridUnitType.Pixel);
        }
    }

    private void OnVerticalSplitterPointerReleased(PointerEventArgs e)
    {
        VerticalSplitter.Classes.Remove("dragging");
        e.Pointer.Capture(null);

        if (DataContext is not MainVM mainVm)
            return;

        if (_isSidebarCollapsedPreview)
        {
            _isSidebarCollapsedPreview = false;
            _savedSidebarWidth = _preDragSidebarWidth;
            MainGrid.ColumnDefinitions[1].MinWidth = 0;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
            SidebarGrid.IsVisible = false;
            mainVm.ActiveSidebarTab = SidebarTab.None;
        }
        else
        {
            double deltaX = e.GetPosition(MainGrid).X - _sidebarDragStartCursorX;
            double rawWidth = _sidebarDragStartWidth + deltaX;
            double maxWidth = Math.Max(MinSidebarWidth, MainGrid.Bounds.Width - 100);
            double finalWidth = Math.Clamp(rawWidth, MinSidebarWidth, maxWidth);
            _savedSidebarWidth = finalWidth;
            SidebarGrid.IsVisible = true;
            MainGrid.ColumnDefinitions[1].MinWidth = MinSidebarWidth;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(finalWidth, GridUnitType.Pixel);
        }
    }

    private void OnVerticalSplitterPointerCaptureLost()
    {
        VerticalSplitter.Classes.Remove("dragging");

        if (DataContext is not MainVM mainVm)
            return;

        if (_isSidebarCollapsedPreview)
        {
            _isSidebarCollapsedPreview = false;
            _savedSidebarWidth = _preDragSidebarWidth;
            MainGrid.ColumnDefinitions[1].MinWidth = 0;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
            SidebarGrid.IsVisible = false;
            mainVm.ActiveSidebarTab = SidebarTab.None;
        }
        else
        {
            double currentWidth = MainGrid.ColumnDefinitions.Count >= 2 && MainGrid.ColumnDefinitions[1].Width.IsAbsolute
                ? Math.Max(MinSidebarWidth, MainGrid.ColumnDefinitions[1].Width.Value)
                : _savedSidebarWidth;
            _savedSidebarWidth = currentWidth;
            SidebarGrid.IsVisible = true;
            MainGrid.ColumnDefinitions[1].MinWidth = MinSidebarWidth;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(currentWidth, GridUnitType.Pixel);
        }
    }

    private void OnHorizontalSplitterPointerMoved(PointerEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainVM mainVm || !mainVm.IsBottomPanelVisible || mainVm.IsBottomPanelMaximized)
            return;

        double deltaY = _bottomPanelDragStartCursorY - e.GetPosition(MainGrid).Y;
        double rawHeight = _bottomPanelDragStartHeight + deltaY;

        const double collapseThreshold = MinBottomPanelHeight / 2.0; // 37.5
        if (rawHeight < collapseThreshold)
        {
            if (!_isBottomPanelCollapsedPreview)
            {
                _isBottomPanelCollapsedPreview = true;
                MainGrid.RowDefinitions[2].MinHeight = 0;
                MainGrid.RowDefinitions[2].Height = new GridLength(0);
                BottomPanelBorder.IsVisible = false;
            }
        }
        else
        {
            _isBottomPanelCollapsedPreview = false;
            BottomPanelBorder.IsVisible = true;
            MainGrid.RowDefinitions[2].MinHeight = MinBottomPanelHeight;
            double maxHeight = Math.Max(MinBottomPanelHeight, MainGrid.Bounds.Height - 100);
            double targetHeight = Math.Clamp(rawHeight, MinBottomPanelHeight, maxHeight);
            MainGrid.RowDefinitions[2].Height = new GridLength(targetHeight, GridUnitType.Pixel);
        }
    }

    private void OnHorizontalSplitterPointerReleased(PointerEventArgs e)
    {
        HorizontalSplitter.Classes.Remove("dragging");
        e.Pointer.Capture(null);

        if (DataContext is not MainVM mainVm)
            return;

        if (_isBottomPanelCollapsedPreview)
        {
            _isBottomPanelCollapsedPreview = false;
            _savedBottomPanelHeight = _preDragBottomPanelHeight;
            MainGrid.RowDefinitions[2].MinHeight = 0;
            MainGrid.RowDefinitions[2].Height = new GridLength(0);
            BottomPanelBorder.IsVisible = false;
            mainVm.CloseBottomPanel();
        }
        else
        {
            double deltaY = _bottomPanelDragStartCursorY - e.GetPosition(MainGrid).Y;
            double rawHeight = _bottomPanelDragStartHeight + deltaY;
            double maxHeight = Math.Max(MinBottomPanelHeight, MainGrid.Bounds.Height - 100);
            double finalHeight = Math.Clamp(rawHeight, MinBottomPanelHeight, maxHeight);
            _savedBottomPanelHeight = finalHeight;
            BottomPanelBorder.IsVisible = true;
            MainGrid.RowDefinitions[2].MinHeight = MinBottomPanelHeight;
            MainGrid.RowDefinitions[2].Height = new GridLength(finalHeight, GridUnitType.Pixel);
        }
    }

    private void OnHorizontalSplitterPointerCaptureLost()
    {
        HorizontalSplitter.Classes.Remove("dragging");

        if (DataContext is not MainVM mainVm)
            return;

        if (_isBottomPanelCollapsedPreview)
        {
            _isBottomPanelCollapsedPreview = false;
            _savedBottomPanelHeight = _preDragBottomPanelHeight;
            MainGrid.RowDefinitions[2].MinHeight = 0;
            MainGrid.RowDefinitions[2].Height = new GridLength(0);
            BottomPanelBorder.IsVisible = false;
            mainVm.CloseBottomPanel();
        }
        else
        {
            double currentHeight = MainGrid.RowDefinitions.Count >= 3 && MainGrid.RowDefinitions[2].Height.IsAbsolute
                ? Math.Max(MinBottomPanelHeight, MainGrid.RowDefinitions[2].Height.Value)
                : _savedBottomPanelHeight;
            _savedBottomPanelHeight = currentHeight;
            BottomPanelBorder.IsVisible = true;
            MainGrid.RowDefinitions[2].MinHeight = MinBottomPanelHeight;
            MainGrid.RowDefinitions[2].Height = new GridLength(currentHeight, GridUnitType.Pixel);
        }
    }

    private void UpdateBottomPanelLayout(MainVM vm)
    {
        if (MainGrid.RowDefinitions.Count < 3)
            return;

        if (!vm.IsBottomPanelVisible)
        {
            SaveCurrentBottomPanelHeight();
            MainGrid.RowDefinitions[2].MinHeight = 0;
            MainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            MainGrid.RowDefinitions[2].Height = new GridLength(0);
            BottomPanelBorder.IsVisible = false;
        }
        else if (vm.IsBottomPanelMaximized)
        {
            SaveCurrentBottomPanelHeight();
            MainGrid.RowDefinitions[2].MinHeight = 0;
            MainGrid.RowDefinitions[1].Height = new GridLength(0);
            MainGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
            BottomPanelBorder.IsVisible = true;
        }
        else
        {
            double targetHeight = _savedBottomPanelHeight >= MinBottomPanelHeight ? _savedBottomPanelHeight : 220;
            MainGrid.RowDefinitions[2].MinHeight = MinBottomPanelHeight;
            MainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            MainGrid.RowDefinitions[2].Height = new GridLength(targetHeight, GridUnitType.Pixel);
            BottomPanelBorder.IsVisible = true;
        }
    }

    private void SaveCurrentBottomPanelHeight()
    {
        if (_isBottomPanelCollapsedPreview || HorizontalSplitter.Classes.Contains("dragging"))
            return;

        if (_subscribedMainVm != null && _subscribedMainVm.IsBottomPanelVisible && !_subscribedMainVm.IsBottomPanelMaximized)
        {
            if (MainGrid.RowDefinitions.Count >= 3)
            {
                var row2Height = MainGrid.RowDefinitions[2].Height;
                if (row2Height.IsAbsolute && row2Height.Value >= MinBottomPanelHeight)
                {
                    _savedBottomPanelHeight = row2Height.Value;
                }
                else if (BottomPanelBorder.Bounds.Height >= MinBottomPanelHeight)
                {
                    _savedBottomPanelHeight = BottomPanelBorder.Bounds.Height;
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
            MainGrid.ColumnDefinitions[1].MinWidth = 0;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
            SidebarGrid.IsVisible = false;
        }
        else
        {
            double targetWidth = _savedSidebarWidth >= MinSidebarWidth ? _savedSidebarWidth : 240;
            MainGrid.ColumnDefinitions[1].MinWidth = MinSidebarWidth;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(targetWidth, GridUnitType.Pixel);
            SidebarGrid.IsVisible = true;
        }
    }

    private void SaveCurrentSidebarWidth()
    {
        if (_isSidebarCollapsedPreview || VerticalSplitter.Classes.Contains("dragging"))
            return;

        if (_subscribedMainVm != null && _subscribedMainVm.IsSidebarVisible)
        {
            if (MainGrid.ColumnDefinitions.Count >= 2)
            {
                var col1 = MainGrid.ColumnDefinitions[1];
                if (col1.Width.IsAbsolute && col1.Width.Value >= MinSidebarWidth)
                {
                    _savedSidebarWidth = col1.Width.Value;
                }
                else if (SidebarGrid.Bounds.Width >= MinSidebarWidth)
                {
                    _savedSidebarWidth = SidebarGrid.Bounds.Width;
                }
            }
        }
    }
}