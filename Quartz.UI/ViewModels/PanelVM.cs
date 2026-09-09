using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quartz.UI.Enums;
using Quartz.UI.Tools;

namespace Quartz.UI.ViewModels;

public partial class PanelVM : LayoutRootVM
{
    public ObservableCollection<FileVM> Tabs { get; } = [];
    [ObservableProperty] private FileVM? _selectedTab;
    [ObservableProperty] private Side _newNodeSide = Side.None;

    [ObservableProperty] private double _panelWidth;
    [ObservableProperty] private double _panelHeight;

    private readonly MainVM _mainVM;

    public PanelVM(MainVM mainVM) => _mainVM = mainVM;

    [RelayCommand]
    private void TabReplace(PointerPressedEventArgs? e)
    {
        if (e == null || SelectedTab == null) return;

        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            var pos = e.GetPosition(null);
            _mainVM.IsReadyToDrag = true;
            _mainVM.DragStartX = pos.X;
            _mainVM.DragStartY = pos.Y;
        }
    }

    [RelayCommand]
    private void SelectSide(PointerEventArgs? e)
    {
        if (e == null || e.Source is not Visual visualSource) return;
        if (!_mainVM.IsReadyToDrag) return;

        var currentPos = e.GetPosition(null);
        double distance = Math.Sqrt(Math.Pow(currentPos.X - _mainVM.DragStartX, 2) +
                                    Math.Pow(currentPos.Y - _mainVM.DragStartY, 2));

        if (distance < 8) return;

        // Включаем глобальный режим переноса
        if (!_mainVM.IsTabReplacing)
        {
            _mainVM.IsTabReplacing = true;
            _mainVM.DraggedTab = SelectedTab;
            _mainVM.SourcePanel = this;
        }

        var topLevel = TopLevel.GetTopLevel(visualSource);
        if (topLevel == null) return;

        var hitVisual = topLevel.InputHitTest(currentPos) as Visual;

        // Сбрасываем старые подсветки перед расчетом новых
        _mainVM.ResetAllPanelSides();

        if (hitVisual == null) return;

        // ВАРИАНТ А: Мы ведем мышь над какой-то конкретной вкладкой
        var hitTabItem = hitVisual as TabItem ?? hitVisual.FindAncestorOfType<TabItem>();
        if (hitTabItem != null && hitTabItem.DataContext is FileVM targetTab)
        {
            var positionInTab = e.GetPosition(hitTabItem);
            double w = hitTabItem.Bounds.Width;

            targetTab.InsertSide = positionInTab.X < (w / 2) ? Side.Left : Side.Right;
            return;
        }

        // ВАРИАНТ Б: Мы ведем мышь над телом какой-то панели (TabControl)
        var hitTabControl = hitVisual as TabControl ?? hitVisual.FindAncestorOfType<TabControl>();
        if (hitTabControl != null && hitTabControl.DataContext is PanelVM targetPanel)
        {
            // 1. Ищем контейнер вкладок ОГЛЯДЫВАЯСЬ СВЕРХУ ВНИЗ (он потомок TabControl)
            var itemsPresenter = hitTabControl.FindDescendantOfType<ItemsPresenter>();
            bool isHeaderArea = false;

            if (itemsPresenter != null)
            {
                // Получаем позицию мыши относительно этого контейнера вкладок
                var posInHeader = e.GetPosition(itemsPresenter);

                // Если по вертикали (Y) мы находимся строго в пределах высоты хедера,
                // значит, мышь во воображаемой линии "шапки" (даже если сильно правее табов)
                if (posInHeader.Y >= 0 && posInHeader.Y <= itemsPresenter.Bounds.Height)
                {
                    isHeaderArea = true;
                }
            }

            // 2. Если мы в зоне шапки — подсвечиваем правый бок последней вкладки
            if (isHeaderArea)
            {
                var lastTab = targetPanel.Tabs.LastOrDefault();
                if (lastTab != null)
                {
                    lastTab.InsertSide = Side.Right;
                    return; // Выходим, чтобы не сработали сплиты ниже!
                }
            }

            // 3. Если мы НЕ в шапке, значит мы ниже — над контентом. Работает твоя логика сплитов
            var positionInPanel = e.GetPosition(hitTabControl);
            double w = hitTabControl.Bounds.Width;
            double h = hitTabControl.Bounds.Height;

            if (w < 50 || h < 50) return;

            // Зажигаем превью сплита у ЦЕЛЕВОЙ панели
            if (positionInPanel.X < w / 3) targetPanel.NewNodeSide = Side.Left;
            else if (positionInPanel.X > w * 2 / 3) targetPanel.NewNodeSide = Side.Right;
            else if (positionInPanel.Y < h / 3) targetPanel.NewNodeSide = Side.Top;
            else if (positionInPanel.Y > h * 2 / 3) targetPanel.NewNodeSide = Side.Bottom;
            else targetPanel.NewNodeSide = Side.None;
        }
    }

    [RelayCommand]
    private void TabReplaced(PointerReleasedEventArgs? e)
    {
        _mainVM.IsReadyToDrag = false;

        if (!_mainVM.IsTabReplacing || _mainVM.DraggedTab == null || _mainVM.SourcePanel == null || e == null)
        {
            _mainVM.ResetDragStatus();
            return;
        }

        var tab = _mainVM.DraggedTab;
        var source = _mainVM.SourcePanel;

        if (e.Source is not Visual visualSource) return;
        var topLevel = TopLevel.GetTopLevel(visualSource);
        if (topLevel == null) return;

        // Определяем финальную точку отпускания мыши
        var hitVisual = topLevel.InputHitTest(e.GetPosition(null)) as Visual;

        PanelVM? targetPanel = null;
        FileVM? targetTab = null;
        Side tabSide = Side.None;
        bool droppedInHeader = false; // Флаг: отпустили ли мышь в пустой зоне шапки

        if (hitVisual == null)
        {
            _mainVM.ResetDragStatus();
            return;
        }

        // Выясняем, куда именно бросили вкладку
        var hitTabItem = hitVisual as TabItem ?? hitVisual.FindAncestorOfType<TabItem>();
        if (hitTabItem != null && hitTabItem.DataContext is FileVM fVM)
        {
            targetTab = fVM;
            targetPanel = fVM.Panel;
            tabSide = fVM.InsertSide;
        }
        else
        {
            var hitTabControl = hitVisual as TabControl ?? hitVisual.FindAncestorOfType<TabControl>();
            if (hitTabControl != null && hitTabControl.DataContext is PanelVM pVM)
            {
                targetPanel = pVM;

                // ПРОВЕРКА: А не в пустую ли зону шапки мы её бросили?
                var itemsPresenter = hitTabControl.FindDescendantOfType<ItemsPresenter>();
                if (itemsPresenter != null)
                {
                    var posInHeader = e.GetPosition(itemsPresenter);
                    if (posInHeader.Y >= 0 && posInHeader.Y <= itemsPresenter.Bounds.Height)
                    {
                        droppedInHeader = true; // Да, это шапка!
                    }
                }
            }
        }

        if (targetPanel == null)
        {
            _mainVM.ResetDragStatus();
            return;
        }

        // Исполняем перемещение
        if (targetTab != null)
        {
            // Дроп на другую вкладку (локальная сортировка)
            if (source == targetPanel && tab == targetTab)
            {
                _mainVM.ResetDragStatus();
                return;
            }

            source.Tabs.Remove(tab);
            int targetIndex = targetPanel.Tabs.IndexOf(targetTab);
            if (tabSide == Side.Right) targetIndex++;

            var sourceIndex = source.Tabs.IndexOf(tab);
            if (source == targetPanel && sourceIndex >= 0 && sourceIndex < targetIndex) targetIndex--;
            if (targetIndex < 0) targetIndex = 0;

            if (targetIndex >= targetPanel.Tabs.Count) targetPanel.Tabs.Add(tab);
            else targetPanel.Tabs.Insert(targetIndex, tab);

            targetPanel.SelectedTab = tab;
            tab.Panel = targetPanel;
        }
        else
        {
            // Дроп на тело панели (Сплит или добавление в конец)
            var side = targetPanel.NewNodeSide;

            if (droppedInHeader)
            {
                // Если дропнули в шапку — это железно добавление в конец, никаких сплитов!
                side = Side.None;
            }
            else if (source == targetPanel && side == Side.None)
            {
                // А вот если это дроп в ЦЕНТР (тело) своей же панели — вот тогда ничего не делаем
                _mainVM.ResetDragStatus();
                return;
            }

            // Удаляем из старого места и пушим в конец
            source.Tabs.Remove(tab);

            if (side == Side.None)
            {
                targetPanel.Tabs.Add(tab); // ObservableCollection сама кинет её в самый конец индекса
                targetPanel.SelectedTab = tab;
                tab.Panel = targetPanel;
            }
            else
            {
                // Логика создания новой панели при сплите
                var newPanel = new PanelVM(_mainVM);
                newPanel.Tabs.Add(tab);
                newPanel.SelectedTab = tab;
                tab.Panel = newPanel;

                _mainVM.SplitPanel(targetPanel, newPanel, side);
            }
        }

        // Очистка пустых панелей
        if (source.Tabs.Count == 0 && source != _mainVM.RootNode)
        {
            _mainVM.RemoveEmptyPanel(source);
        }

        _mainVM.ResetDragStatus();
    }

    [RelayCommand]
    protected void SelectPanel() => _mainVM.ActivePanel = this;

    [RelayCommand]
    private void ResetSide() => NewNodeSide = Side.None;
}