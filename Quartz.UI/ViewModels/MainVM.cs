using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quartz.Application.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Models.BoardEntities.Styles;
using Quartz.UI.Enums;
using Quartz.UI.Services;

namespace Quartz.UI.ViewModels;

public class NavigationPoint
{
    public FileVM File { get; }
    public int Line { get; set; }
    public int Column { get; set; }

    public NavigationPoint(FileVM file, int line, int column)
    {
        File = file;
        Line = line;
        Column = column;
    }
}

public partial class MainVM : ObservableObject
{
    [ObservableProperty] private LayoutRootVM? _rootNode;

    [ObservableProperty] private bool _isTabReplacing;
    [ObservableProperty] private bool _isFileReplacing;

    public PanelVM? SourcePanel { get; set; }
    [ObservableProperty] private FileVM? _draggedTab;
    public FileStructVM? TappedFile { get; set; }
    [ObservableProperty] private FileStructVM? _draggedFile;

    [ObservableProperty] private PanelVM? _activePanel;
    [ObservableProperty] private FileVM? _activeFile;

    private readonly List<NavigationPoint> _navigationHistory = new();
    private int _currentHistoryIndex = -1;
    private bool _isNavigatingHistory;
    private readonly DispatcherTimer _navDebounceTimer;

    [ObservableProperty]
    private bool _canNavigateBack;

    [ObservableProperty]
    private bool _canNavigateForward;

    public bool IsReadyToDrag { get; set; }
    public double DragStartX { get; set; }
    public double DragStartY { get; set; }

    [ObservableProperty] private double _fakeFileX;
    [ObservableProperty] private double _fakeFileY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHorizontalSplitterVisible))]
    private bool _isBottomPanelVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHorizontalSplitterVisible))]
    private bool _isBottomPanelMaximized = false;

    public bool IsHorizontalSplitterVisible => IsBottomPanelVisible && !IsBottomPanelMaximized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSidebarVisible))]
    [NotifyPropertyChangedFor(nameof(IsExplorerTabActive))]
    [NotifyPropertyChangedFor(nameof(IsSearchTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTracingTabActive))]
    [NotifyPropertyChangedFor(nameof(SidebarHeaderTitle))]
    private SidebarTab _activeSidebarTab = SidebarTab.Explorer;

    public bool IsSidebarVisible => ActiveSidebarTab != SidebarTab.None;
    public bool IsExplorerTabActive => ActiveSidebarTab == SidebarTab.Explorer;
    public bool IsSearchTabActive => ActiveSidebarTab == SidebarTab.Search;
    public bool IsTracingTabActive => ActiveSidebarTab == SidebarTab.Tracing;

    public string ProjectName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_projectRootDir))
                return "ПРОВОДНИК";

            try
            {
                var dirInfo = new DirectoryInfo(_projectRootDir);
                var name = dirInfo.Name;
                return !string.IsNullOrWhiteSpace(name) ? name.ToUpperInvariant() : "ПРОВОДНИК";
            }
            catch
            {
                return "ПРОВОДНИК";
            }
        }
    }

    public string SidebarHeaderTitle =>
        ActiveSidebarTab switch
        {
            SidebarTab.Explorer => ProjectName,
            SidebarTab.Search => "ПОИСК",
            SidebarTab.Tracing => "ТРАССИРОВКА",
            _ => string.Empty
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProblemsTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTerminalTabActive))]
    private BottomPanelTab _activeBottomTab = BottomPanelTab.Problems;

    public bool IsProblemsTabActive => ActiveBottomTab == BottomPanelTab.Problems;
    public bool IsTerminalTabActive => ActiveBottomTab == BottomPanelTab.Terminal;

    [ObservableProperty]
    private EditorVM? _activeEditor;

    public string CurrentCaretPositionText =>
        ActiveEditor != null
            ? $"Строка {ActiveEditor.CaretLine}, столбец {ActiveEditor.CaretColumn}"
            : "Строка 1, столбец 1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IndentationText))]
    private int _indentationSize = 2;

    public string IndentationText => $"Пробелов: {IndentationSize}";

    public int TotalErrorCount => ProjectErrors.Sum(f => f.TotalIssueCount);

    private readonly IBoardProcessingCoordinator _boardProcessingCoordinator;
    private readonly IFolderDialogService _folderDialogService;

    private string? _projectRootDir;
    public string? ProjectRootDir => _projectRootDir;
    public bool HasOpenFolder => !string.IsNullOrWhiteSpace(_projectRootDir) && _projectRootDirectory != null;

    public bool IsAnyFolderExpanded
    {
        get
        {
            if (_projectRootDirectory == null) return false;
            return CheckAnyFolderExpanded(_projectRootDirectory);
        }
    }

    private static bool CheckAnyFolderExpanded(DirectoryVM dir)
    {
        if (dir.IsExpanded) return true;
        foreach (var child in dir.Children)
        {
            if (child is DirectoryVM subDir && CheckAnyFolderExpanded(subDir))
                return true;
        }
        return false;
    }

    public string ExpandAllToolTip => IsAnyFolderExpanded ? "Свернуть все" : "Развернуть все";

    public void NotifyFolderExpandedChanged()
    {
        OnPropertyChanged(nameof(IsAnyFolderExpanded));
        OnPropertyChanged(nameof(ExpandAllToolTip));
    }

    [RelayCommand]
    public void ToggleExpandAll()
    {
        if (_projectRootDirectory == null) return;
        bool shouldExpand = !IsAnyFolderExpanded;
        SetExpandedRecursive(_projectRootDirectory, shouldExpand);
        NotifyFolderExpandedChanged();
    }

    private static void SetExpandedRecursive(DirectoryVM dir, bool isExpanded)
    {
        dir.IsExpanded = isExpanded;
        foreach (var child in dir.Children)
        {
            if (child is DirectoryVM subDir)
            {
                SetExpandedRecursive(subDir, isExpanded);
            }
        }
    }

    public ObservableCollection<FileVM> ProjectErrors { get; } = [];

    public MainVM(
        IBoardProcessingCoordinator boardProcessingCoordinator,
        IFolderDialogService folderDialogService)
    {
        _boardProcessingCoordinator = boardProcessingCoordinator;
        _folderDialogService = folderDialogService;

        _navDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _navDebounceTimer.Tick += OnNavDebounceTimerTick;

        _fsDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _fsDebounceTimer.Tick += OnFsDebounceTimerTick;

        ProjectErrors.CollectionChanged += OnProjectErrorsCollectionChanged;

        var defaultProjectDir = Path.Combine("/", "home", "alexandr", "Games", "Проект");
        if (Directory.Exists(defaultProjectDir))
        {
            LoadProjectFromDirectory(defaultProjectDir);
        }
    }

    private DirectoryVM? _projectRootDirectory;
    public DirectoryVM? ProjectRootDirectory => _projectRootDirectory;
    private static readonly ObservableCollection<FileStructVM> _emptyTree = [];
    public ObservableCollection<FileStructVM> FileTree => _projectRootDirectory?.Children ?? _emptyTree;

    private readonly Dictionary<string, LayerVM> _projectLayers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, StylesFileVM> _projectStyles = new(StringComparer.OrdinalIgnoreCase);

    public BoardVM? CurrentBoard { get; set; }

    /// <summary>
    /// Разделяет целевую панель на две, вставляя между ними сплиттер.
    /// </summary>
    public void SplitPanel(PanelVM targetPanel, PanelVM newPanel, Side side)
    {
        if (RootNode == null)
            return;

        if (RootNode == targetPanel)
        {
            var newRoot = CreateSplitContainer(targetPanel, newPanel, side);
            if (newRoot != null)
            {
                newRoot.Size = "1*"; // Корень всегда занимает всю доступную область
                RootNode = newRoot;
            }

            return;
        }

        var parent = FindParentContainer(RootNode, targetPanel);
        if (parent is null)
            return;

        // 1. ВАЖНО: Запоминаем текущий размер (пропорцию) целевой панели, 
        // пока она еще находится в родительском контейнере
        string oldSize = targetPanel.Size;

        var splitContainer = CreateSplitContainer(targetPanel, newPanel, side);
        if (splitContainer is null)
            return;

        // 2. ВАЖНО: Передаем этот размер новому сплит-контейнеру.
        // Теперь он займет ровно то место (и в тех же пикселях/долях), которое занимала панель
        splitContainer.Size = oldSize;

        // Находим индекс старой панели у родителя и подменяем её на новый сплит-контейнер
        int index = parent.Children.IndexOf(targetPanel);
        if (index != -1)
            parent.Children[index] = splitContainer;
    }

    /// <summary>
    /// Вспомогательный метод: создает сплиттер и упаковывает в него две панели в нужном порядке.
    /// </summary>
    private SplitContainerVM? CreateSplitContainer(PanelVM target, PanelVM newPanel, Side side)
    {
        var container = new SplitContainerVM();

        // отдаем по 50% каждому окну
        target.Size = "1*";
        newPanel.Size = "1*";

        switch (side)
        {
            case Side.Left:
                container.IsHorizontal = true; // Сплит на колонки
                container.Children.Add(newPanel);
                container.Children.Add(target);
                break;

            case Side.Right:
                container.IsHorizontal = true; // Сплит на колонки
                container.Children.Add(target);
                container.Children.Add(newPanel);
                break;

            case Side.Top:
                container.IsHorizontal = false; // Сплит на строки
                container.Children.Add(newPanel);
                container.Children.Add(target);
                break;

            case Side.Bottom:
                container.IsHorizontal = false; // Сплит на строки
                container.Children.Add(target);
                container.Children.Add(newPanel);
                break;

            default:
                return null;
        }

        return container;
    }

    /// <summary>
    /// Рекурсивный поиск родительского контейнера для указанного узла.
    /// </summary>
    private SplitContainerVM? FindParentContainer(LayoutRootVM current, LayoutRootVM targetToFind)
    {
        if (current is not SplitContainerVM container)
            return null;

        // Проверяем, нет ли искомого элемента среди прямых потомков
        if (container.Children.Contains(targetToFind))
            return container;

        // Идем вглубь по дереву рекурсивно
        foreach (var child in container.Children)
        {
            var found = FindParentContainer(child, targetToFind);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Удаляет опустевшую панель из дерева и схлопывает родительский сплиттер, если он больше не нужен.
    /// </summary>
    public void RemoveEmptyPanel(PanelVM emptyPanel)
    {
        if (RootNode == null || RootNode == emptyPanel)
        {
            RootNode = null;
            ActivePanel = null;
            return;
        }

        var parent = FindParentContainer(RootNode, emptyPanel);
        if (parent != null)
        {
            var neighbor = parent.Children.FirstOrDefault(c => c != emptyPanel);

            // 2. Удаляем пустую панель из родителя
            parent.Children.Remove(emptyPanel);

            // 3. Вычисляем новую ActivePanel на основе выжившего соседа
            if (neighbor is PanelVM neighborPanel)
            {
                ActivePanel = neighborPanel;
            }
            else if (neighbor is SplitContainerVM neighborContainer)
            {
                ActivePanel = FindFirstPanel(neighborContainer);
            }

            // Если в родительском сплиттере остался всего один элемент,
            // то сам этот сплиттер больше не нужен — вытаскиваем оставшийся элемент уровнем выше!
            if (parent.Children.Count == 1)
            {
                var remainingChild = parent.Children[0];

                if (RootNode == parent)
                {
                    RootNode = remainingChild;
                    RootNode.Size = "1*";
                }
                else
                {
                    var grandParent = FindParentContainer(RootNode, parent);
                    if (grandParent != null)
                    {
                        int index = grandParent.Children.IndexOf(parent);
                        if (index != -1)
                        {
                            grandParent.Children[index] = remainingChild;
                            remainingChild.Size = parent.Size; // Сохраняем пропорции родителя
                        }
                    }
                }
            }
        }
    }

    private PanelVM? FindFirstPanel(LayoutRootVM node)
    {
        if (node is PanelVM panel)
            return panel;

        if (node is SplitContainerVM container)
        {
            foreach (var child in container.Children)
            {
                var found = FindFirstPanel(child);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    [RelayCommand]
    public void OpenFileTab(FileVM file)
    {
        if (file is EditorVM editor)
        {
            editor.LoadDocumentIfEmpty();
        }

        file.UpdateBreadcrumbs();

        if (file.Panel != null)
        {
            ActivePanel = file.Panel;
            file.Panel.SelectedTab = file;
            ActiveEditor = file as EditorVM;
            ActiveFile = file;
            return;
        }

        if (ActivePanel is null && RootNode != null)
        {
            ActivePanel = FindFirstPanel(RootNode);
        }

        if (ActivePanel is null || RootNode is null)
        {
            var initialPanel = new PanelVM(this) { Size = "1*" };
            RootNode = initialPanel;
            ActivePanel = initialPanel;
        }

        ActivePanel.Tabs.Add(file);
        ActivePanel.SelectedTab = file;
        file.Panel = ActivePanel;
        ActiveEditor = file as EditorVM;
        ActiveFile = file;
    }

    [RelayCommand]
    public void NavigateToError(object? parameter)
    {
        if (parameter is Problem problem)
        {
            var file = ProjectErrors.FirstOrDefault(f => f.Problems.Contains(problem));
            if (file != null)
            {
                NavigateToProblem(file, problem);
            }
        }
        else if (parameter is FileVM file)
        {
            OpenFileTab(file);
        }
    }

    public void NavigateToProblem(FileVM file, Problem problem)
    {
        if (IsBottomPanelMaximized)
        {
            IsBottomPanelMaximized = false;
        }

        if (ActiveFile != null && ActiveEditor != null)
        {
            RecordNavigationPoint(ActiveFile, ActiveEditor.CaretLine, ActiveEditor.CaretColumn);
        }

        OpenFileTab(file);

        if (file is EditorVM editor)
        {
            editor.RequestNavigateTo(problem.Line, problem.Column);
            RecordNavigationPoint(file, (int)problem.Line, (int)problem.Column);
        }
    }

    public void NavigateToError(FileVM file, EditorError error) => NavigateToProblem(file, error);

    [RelayCommand]
    public void CloseBottomPanel()
    {
        IsBottomPanelVisible = false;
        IsBottomPanelMaximized = false;
    }

    [RelayCommand]
    public void ToggleBottomPanel()
    {
        if (IsBottomPanelVisible)
        {
            if (ActiveBottomTab != BottomPanelTab.Problems)
            {
                ActiveBottomTab = BottomPanelTab.Problems;
            }
            else
            {
                IsBottomPanelVisible = false;
                IsBottomPanelMaximized = false;
            }
        }
        else
        {
            IsBottomPanelVisible = true;
            ActiveBottomTab = BottomPanelTab.Problems;
        }
    }

    [RelayCommand]
    public void ToggleMaximizeBottomPanel()
    {
        if (!IsBottomPanelVisible)
        {
            IsBottomPanelVisible = true;
        }

        IsBottomPanelMaximized = !IsBottomPanelMaximized;
    }

    [RelayCommand]
    public void SelectProblemsTab()
    {
        ActiveBottomTab = BottomPanelTab.Problems;
    }

    [RelayCommand]
    public void SelectTerminalTab()
    {
        ActiveBottomTab = BottomPanelTab.Terminal;
    }

    [RelayCommand]
    public void SelectExplorerTab()
    {
        ActiveSidebarTab = ActiveSidebarTab == SidebarTab.Explorer ? SidebarTab.None : SidebarTab.Explorer;
    }

    [RelayCommand]
    public void SelectSearchTab()
    {
        ActiveSidebarTab = ActiveSidebarTab == SidebarTab.Search ? SidebarTab.None : SidebarTab.Search;
    }

    [RelayCommand]
    public void SelectTracingTab()
    {
        ActiveSidebarTab = ActiveSidebarTab == SidebarTab.Tracing ? SidebarTab.None : SidebarTab.Tracing;
    }

    [RelayCommand]
    public void ToggleIndentationSize()
    {
        IndentationSize = IndentationSize == 2 ? 4 : 2;
        if (ActiveEditor != null)
        {
            ActiveEditor.IndentationSize = IndentationSize;
        }
    }

    partial void OnActivePanelChanged(PanelVM? oldValue, PanelVM? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnActivePanelPropertyChanged;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnActivePanelPropertyChanged;
        }

        UpdateActiveEditor();
    }

    private void OnActivePanelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelVM.SelectedTab))
        {
            UpdateActiveEditor();
        }
    }

    private void UpdateActiveEditor()
    {
        ActiveEditor = ActivePanel?.SelectedTab as EditorVM;
        ActiveFile = ActivePanel?.SelectedTab;
        UpdateBreadcrumbs();
    }

    public void UpdateBreadcrumbs()
    {
        ActiveFile?.UpdateBreadcrumbs();

        if (RootNode != null)
        {
            UpdateBreadcrumbsRecursive(RootNode);
        }
    }

    private void UpdateBreadcrumbsRecursive(LayoutRootVM node)
    {
        if (node is PanelVM panel)
        {
            foreach (var tab in panel.Tabs)
            {
                tab.UpdateBreadcrumbs();
            }
        }
        else if (node is SplitContainerVM container)
        {
            foreach (var child in container.Children)
            {
                UpdateBreadcrumbsRecursive(child);
            }
        }
    }

    partial void OnActiveEditorChanged(EditorVM? oldValue, EditorVM? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnActiveEditorPropertyChanged;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnActiveEditorPropertyChanged;
            IndentationSize = newValue.IndentationSize;

            if (!_isNavigatingHistory)
            {
                RecordNavigationPoint(newValue, newValue.CaretLine, newValue.CaretColumn);
            }
        }

        OnPropertyChanged(nameof(CurrentCaretPositionText));
        OnPropertyChanged(nameof(IndentationText));
    }

    private void OnActiveEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorVM.CaretLine) or nameof(EditorVM.CaretColumn))
        {
            OnPropertyChanged(nameof(CurrentCaretPositionText));
            if (!_isNavigatingHistory && ActiveEditor != null && ActiveFile != null)
            {
                OnCaretMoved(ActiveFile, ActiveEditor.CaretLine, ActiveEditor.CaretColumn);
            }
        }
        else if (e.PropertyName == nameof(EditorVM.IndentationSize))
        {
            if (ActiveEditor != null)
            {
                IndentationSize = ActiveEditor.IndentationSize;
            }
            OnPropertyChanged(nameof(IndentationText));
        }
    }

    private void OnProjectErrorsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (FileVM file in e.NewItems.OfType<FileVM>())
            {
                file.Errors.CollectionChanged += OnFileErrorsCollectionChanged;
                file.Warnings.CollectionChanged += OnFileErrorsCollectionChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (FileVM file in e.OldItems.OfType<FileVM>())
            {
                file.Errors.CollectionChanged -= OnFileErrorsCollectionChanged;
                file.Warnings.CollectionChanged -= OnFileErrorsCollectionChanged;
            }
        }
        OnPropertyChanged(nameof(TotalErrorCount));
    }

    private void OnFileErrorsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TotalErrorCount));
    }

    /// <summary>
    /// Публичный метод для запуска сброса подсветок со всей разметки
    /// </summary>
    public void ResetAllPanelSides(PanelVM? exceptPanel = null, FileVM? exceptTab = null)
    {
        if (RootNode != null)
            ResetSidesRecursive(RootNode, exceptPanel, exceptTab);
    }

    /// <summary>
    /// Рекурсивный обход дерева панелей для обнуления NewNodeSide
    /// </summary>
    private void ResetSidesRecursive(LayoutRootVM node, PanelVM? exceptPanel = null, FileVM? exceptTab = null)
    {
        if (node is PanelVM panel)
        {
            if (panel != exceptPanel && panel.NewNodeSide != Side.None)
            {
                panel.NewNodeSide = Side.None;
            }
            // Очищаем синие полосочки у ВСЕХ вкладок этой панели
            foreach (var tab in panel.Tabs)
            {
                if (tab != exceptTab && tab.InsertSide != Side.None)
                {
                    tab.InsertSide = Side.None;
                }
            }
        }
        else if (node is SplitContainerVM container)
        {
            foreach (var child in container.Children)
            {
                ResetSidesRecursive(child, exceptPanel, exceptTab);
            }
        }
    }

    [RelayCommand]
    public void ResetDragStatus()
    {
        IsReadyToDrag = false;
        IsTabReplacing = false;
        IsFileReplacing = false;
        DraggedTab = null;
        DraggedFile = null;
        TappedFile = null;
        SourcePanel = null;
        ResetAllPanelSides();
    }

    private DirectoryVM LoadDirectories(string path, DirectoryVM? parent)
    {
        var dir = new DirectoryVM(mainVM: this, parent: parent, filePath: path);

        try
        {
            var directories = Directory
                .GetDirectories(dir.FilePath)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var d in directories)
            {
                dir.Children.Add(LoadDirectories(d, dir));
            }

            var files = Directory
                .GetFiles(dir.FilePath)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                var file = LoadFile(f, dir);
                if (file != null)
                    dir.Children.Add(file);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка доступа к {path}: {ex.Message}");
        }

        return dir;
    }

    private FileVM? LoadFile(string path, DirectoryVM? parent)
    {
        var ext = Path.GetExtension(path);
        string relPath = GetRelativePath(path);

        switch (ext)
        {
            case ".pcby":
                var board = new BoardVM(mainVM: this, parent: parent, filePath: path,
                    coordinator: _boardProcessingCoordinator)
                {
                    FilePath = path
                };
                CurrentBoard = board;
                return board;

            case ".layy":
                var layer = new LayerVM(mainVM: this, parent: parent, filePath: path,
                    coordinator: _boardProcessingCoordinator)
                {
                    FilePath = path
                };

                // ЗАПОМИНАЕМ СЛОЙ СРАЗУ ПРИ ОТКРЫТИИ ПРОЕКТА

                _projectLayers[relPath] = layer;

                return layer;

            case ".stly":
                var stylesFile = new StylesFileVM(mainVM: this, parent: parent, filePath: path,
                    coordinator: _boardProcessingCoordinator)
                {
                    FilePath = path
                };

                // ЗАПОМИНАЕМ ФАЙЛ СТИЛЕЙ СРАЗУ ПРИ ОТКРЫТИИ ПРОЕКТА
                _projectStyles[relPath] = stylesFile;

                return stylesFile;

            default:
                var plainFile = new PlainFileVM(
                    mainVM: this,
                    filePath: path,
                    parent: parent,
                    coordinator: _boardProcessingCoordinator)
                {
                    FilePath = path
                };
                return plainFile;
        }
    }

    [RelayCommand]
    private void MoveFile(PointerEventArgs? e)
    {
        if (e is null)
            return;

        // апдейт координат для отрисовки перемещаемого файла
        var currentPos = e.GetPosition(null);
        FakeFileX = currentPos.X + 6;
        FakeFileY = currentPos.Y + 6;

        if (TappedFile is null)
            return;

        if (!IsFileReplacing)
        {
            double distance = Math.Sqrt(Math.Pow(currentPos.X - DragStartX, 2) +
                                        Math.Pow(currentPos.Y - DragStartY, 2));

            // Если сдвиг меньше 8 пикселей — игнорируем (защита от кликов и микротремора)
            if (distance < 8)
                return;

            IsFileReplacing = true;
        }

        if (DraggedFile != TappedFile)
            DraggedFile = TappedFile;
    }

    [RelayCommand]
    private void FileReplace(PointerPressedEventArgs? e)
    {
        if (e?.Source is not Visual visualSource)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (treeViewItem?.DataContext is not FileStructVM dc)
            return;

        dc.TapStartCommand.Execute(e);
    }

    [RelayCommand]
    private void FileReplaced(PointerReleasedEventArgs? e)
    {
        if (e?.Source is not Visual visualSource)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (treeViewItem?.DataContext is not FileStructVM dc)
            return;

        dc.TapEndCommand.Execute(null);
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        var selectedPath = await _folderDialogService.OpenFolderAsync("Открыть проект Quartz");
        if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            return;

        LoadProjectFromDirectory(selectedPath);
    }

    [RelayCommand]
    private async Task OpenSingleFile()
    {
        var selectedPath = await _folderDialogService.OpenFileAsync("Открыть файл");
        if (string.IsNullOrWhiteSpace(selectedPath) || !File.Exists(selectedPath))
            return;

        CloseProject();

        var file = LoadFile(selectedPath, parent: null);
        if (file != null)
        {
            OpenFileTab(file);

            if (CurrentBoard != null)
            {
                _ = CurrentBoard.ProcessBoardProject();
            }
        }
    }

    [RelayCommand]
    public void CloseProject()
    {
        StopFileWatcher();

        _projectLayers.Clear();
        _projectStyles.Clear();
        _projectRootDir = null;
        _projectRootDirectory = null;
        CurrentBoard = null;

        ProjectErrors.Clear();

        RootNode = null;
        ActivePanel = null;
        ActiveEditor = null;
        ActiveFile = null;

        _navigationHistory.Clear();
        _currentHistoryIndex = -1;
        UpdateCanNavigate();

        OnPropertyChanged(nameof(ProjectRootDir));
        OnPropertyChanged(nameof(HasOpenFolder));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(SidebarHeaderTitle));
        OnPropertyChanged(nameof(FileTree));
        OnPropertyChanged(nameof(TotalErrorCount));
        NotifyFolderExpandedChanged();
    }

    [RelayCommand]
    private void SaveCurrentFile()
    {
        if (ActivePanel?.SelectedTab is EditorVM editor)
        {
            editor.Save();
        }
    }

    [RelayCommand]
    private async Task SaveAsCurrentFileAsync()
    {
        if (ActivePanel?.SelectedTab is EditorVM editor)
        {
            await editor.SaveAsAsync();
        }
    }

    [RelayCommand]
    public void SaveAllFiles()
    {
        if (RootNode == null) return;
        var allPanels = new List<PanelVM>();
        CollectPanels(RootNode, allPanels);
        foreach (var panel in allPanels)
        {
            foreach (var tab in panel.Tabs)
            {
                if (tab is EditorVM editor && editor.IsModified)
                {
                    editor.Save();
                }
            }
        }
    }

    private void CollectPanels(LayoutRootVM node, List<PanelVM> result)
    {
        if (node is PanelVM panel)
        {
            result.Add(panel);
        }
        else if (node is SplitContainerVM container)
        {
            foreach (var child in container.Children)
            {
                CollectPanels(child, result);
            }
        }
    }

    private void OnNavDebounceTimerTick(object? sender, EventArgs e)
    {
        _navDebounceTimer.Stop();
        if (_isNavigatingHistory || ActiveEditor == null || ActiveFile == null) return;

        int line = ActiveEditor.CaretLine;
        int col = ActiveEditor.CaretColumn;

        if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count)
        {
            var cur = _navigationHistory[_currentHistoryIndex];
            if (cur.File == ActiveFile && Math.Abs(cur.Line - line) < 4)
            {
                cur.Line = line;
                cur.Column = col;
                return;
            }
        }

        RecordNavigationPoint(ActiveFile, line, col);
    }

    private void OnCaretMoved(FileVM file, int line, int column)
    {
        if (_isNavigatingHistory) return;

        if (_navigationHistory.Count == 0 || _currentHistoryIndex < 0)
        {
            RecordNavigationPoint(file, line, column);
            return;
        }

        var current = _navigationHistory[_currentHistoryIndex];
        if (current.File != file)
        {
            _navDebounceTimer.Stop();
            RecordNavigationPoint(file, line, column);
            return;
        }

        if (Math.Abs(current.Line - line) >= 5)
        {
            _navDebounceTimer.Stop();
            RecordNavigationPoint(file, line, column);
        }
        else
        {
            current.Line = line;
            current.Column = column;
            _navDebounceTimer.Stop();
            _navDebounceTimer.Start();
        }
    }

    public void RecordNavigationPoint(FileVM file, int line, int column)
    {
        if (_isNavigatingHistory || file == null) return;

        if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count)
        {
            var cur = _navigationHistory[_currentHistoryIndex];
            if (cur.File == file && cur.Line == line && cur.Column == column)
                return;

            if (cur.File == file && Math.Abs(cur.Line - line) <= 2)
            {
                cur.Line = line;
                cur.Column = column;
                return;
            }
        }

        if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count - 1)
        {
            _navigationHistory.RemoveRange(_currentHistoryIndex + 1, _navigationHistory.Count - (_currentHistoryIndex + 1));
        }

        if (_navigationHistory.Count >= 100)
        {
            _navigationHistory.RemoveAt(0);
            _currentHistoryIndex--;
        }

        _navigationHistory.Add(new NavigationPoint(file, line, column));
        _currentHistoryIndex = _navigationHistory.Count - 1;
        UpdateCanNavigate();
    }

    private void UpdateCanNavigate()
    {
        CanNavigateBack = _currentHistoryIndex > 0;
        CanNavigateForward = _currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count - 1;
        ActiveEditor?.NotifyNavigationStateChanged();
    }

    [RelayCommand]
    public void NavigateBack()
    {
        if (!CanNavigateBack || _currentHistoryIndex <= 0) return;

        if (ActiveFile != null && ActiveEditor != null && _currentHistoryIndex < _navigationHistory.Count)
        {
            var cur = _navigationHistory[_currentHistoryIndex];
            if (cur.File != ActiveFile || Math.Abs(cur.Line - ActiveEditor.CaretLine) >= 4)
            {
                _navDebounceTimer.Stop();
                RecordNavigationPoint(ActiveFile, ActiveEditor.CaretLine, ActiveEditor.CaretColumn);
            }
            else
            {
                cur.Line = ActiveEditor.CaretLine;
                cur.Column = ActiveEditor.CaretColumn;
            }
        }

        if (_currentHistoryIndex <= 0) return;

        _currentHistoryIndex--;
        var target = _navigationHistory[_currentHistoryIndex];
        ExecuteNavigation(target);
        UpdateCanNavigate();
    }

    [RelayCommand]
    public void NavigateForward()
    {
        if (!CanNavigateForward || _currentHistoryIndex >= _navigationHistory.Count - 1) return;

        _currentHistoryIndex++;
        var target = _navigationHistory[_currentHistoryIndex];
        ExecuteNavigation(target);
        UpdateCanNavigate();
    }

    private void ExecuteNavigation(NavigationPoint target)
    {
        _isNavigatingHistory = true;
        _navDebounceTimer.Stop();
        try
        {
            OpenFileTab(target.File);
            if (target.File is EditorVM editor)
            {
                editor.RequestNavigateTo(target.Line, target.Column);
            }
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                _isNavigatingHistory = false;
            }, DispatcherPriority.Background);
        }
    }

    [RelayCommand]
    private void CloseActiveTab()
    {
        if (ActivePanel?.SelectedTab is { } tab)
        {
            tab.RemoveTab();
        }
    }

    [RelayCommand]
    private void CloseSavedTabsInActivePanel()
    {
        if (ActivePanel == null) return;
        var tabsToClose = ActivePanel.Tabs
            .Where(t => !t.IsModified && !t.IsPinned)
            .ToList();
        foreach (var tab in tabsToClose)
        {
            tab.RemoveTab();
        }
    }

    [RelayCommand]
    private void CloseAllTabsInActivePanel()
    {
        if (ActivePanel == null) return;
        var tabsToClose = ActivePanel.Tabs
            .Where(t => !t.IsPinned)
            .ToList();
        foreach (var tab in tabsToClose)
        {
            tab.RemoveTab();
        }
    }

    public void LoadProjectFromDirectory(string directoryPath)
    {
        // 1. Очищаем состояние предыдущего проекта
        StopFileWatcher();
        _projectLayers.Clear();
        _projectStyles.Clear();
        _projectRootDirectory = null;
        OnPropertyChanged(nameof(FileTree));
        ProjectErrors.Clear();
        OnPropertyChanged(nameof(TotalErrorCount));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(SidebarHeaderTitle));
        CurrentBoard = null;

        // Сбрасываем разметку редактора и вкладки
        RootNode = null;
        ActivePanel = null;
        ActiveEditor = null;
        ActiveFile = null;

        _navigationHistory.Clear();
        _currentHistoryIndex = -1;
        UpdateCanNavigate();

        // 2. Загружаем файловую структуру выбранной директории
        _projectRootDir = directoryPath;
        _projectRootDirectory = LoadDirectories(_projectRootDir, parent: null);

        OnPropertyChanged(nameof(ProjectRootDir));
        OnPropertyChanged(nameof(HasOpenFolder));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(SidebarHeaderTitle));
        OnPropertyChanged(nameof(FileTree));
        NotifyFolderExpandedChanged();

        // 3. Запускаем наблюдение за файловой системой
        StartFileWatcher(directoryPath);

        // 4. Если в проекте есть плата, запускаем расчет
        if (CurrentBoard != null)
        {
            _ = CurrentBoard.ProcessBoardProject();
        }
    }




    // --- ОПТИМИЗИРОВАННАЯ РАБОТА С ФАЙЛАМИ ---

    /// <summary>
    /// Мгновенно собирает тексты всех слоев без обхода UI и дерева файлов.
    /// </summary>
    public Dictionary<string, string> GetAllLayerTexts()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relPath, layerVm) in _projectLayers)
        {
            // Берем актуальный текст из памяти редактора или с диска
            result[relPath] = layerVm.GetActualText();
        }

        return result;
    }

    public Dictionary<string, string> GetAllStylesTexts()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relPath, stylesFileVm) in _projectStyles)
        {
            // Берем актуальный текст из памяти редактора или с диска
            result[relPath] = stylesFileVm.GetActualText();
        }

        return result;
    }

    /// <summary>
    /// Запуск пересборки проекта платы.
    /// </summary>
    public void ReloadCurrentProject()
    {
        _ = CurrentBoard?.ProcessBoardProject();
    }

    /// <summary>
    /// Рассылает готовые результаты парсинга по зарегистрированным слоям.
    /// </summary>
    public void NotifyLayersBoardUpdated(
        List<DrawingPrimitive> boardOverlayPrimitives,
        IDictionary<string, ProcessResult<LayerModel>> layerResults)
    {
        foreach (var (relPath, layerVM) in _projectLayers)
        {
            if (layerResults.TryGetValue(relPath, out var layerResult))
            {
                // Отдаем слою его результат — пусть сам решает, как себя обновить
                layerVM.ApplyProjectResult(layerResult, boardOverlayPrimitives);

                UpdateFileErrors(layerVM);
            }
        }
    }

    public void NotifyStylesUpdated(
        IDictionary<string,
        ProcessResult<List<Style>>> styleResults)
    {
        foreach (var (relPath, styleVM) in _projectStyles)
        {
            if (styleResults.TryGetValue(relPath, out var styleResult))
            {
                styleVM.ApplyProjectResult(styleResult);

                UpdateFileErrors(styleVM);
            }
        }
    }

    /// <summary>
    /// Обновляет ошибки для конкретного файла в общем дереве ошибок
    /// </summary>
    public void UpdateFileErrors(FileVM fileVM)//string relativeOrFullPath, IEnumerable<EditorError> errors)
    {
        if (fileVM.Problems.Count == 0)
        {
            // Если ошибок нет — удаляем файл из списка TreeView
            ProjectErrors.Remove(fileVM);
        }
        else if (!ProjectErrors.Contains(fileVM))
        {
            // Если файла еще нет в дереве — добавляем
            ProjectErrors.Add(fileVM);
        }
        OnPropertyChanged(nameof(TotalErrorCount));
    }

    public string GetRelativePath(string fullPath)
    {
        if (string.IsNullOrEmpty(_projectRootDir))
            return Path.GetFileName(fullPath);

        return Path.GetRelativePath(_projectRootDir, fullPath).Replace('\\', '/');
    }

    // --- НАБЛЮДЕНИЕ ЗА ФАЙЛОВОЙ СИСТЕМОЙ (FILE SYSTEM WATCHER) ---

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private FileSystemWatcher? _fileWatcher;
    private readonly DispatcherTimer _fsDebounceTimer;
    private readonly HashSet<string> _pendingChangedPaths = new(PathComparer);
    private readonly Dictionary<string, string> _pendingRenames = new(PathComparer);
    private readonly object _fsLock = new();

    private void StartFileWatcher(string dirPath)
    {
        StopFileWatcher();

        try
        {
            _fileWatcher = new FileSystemWatcher(dirPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.CreationTime |
                               NotifyFilters.LastWrite
            };

            _fileWatcher.Created += OnFileSystemChanged;
            _fileWatcher.Deleted += OnFileSystemChanged;
            _fileWatcher.Renamed += OnFileSystemRenamed;
            _fileWatcher.Error += OnFileSystemError;

            _fileWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Не удалось запустить FileSystemWatcher: {ex.Message}");
        }
    }

    private void StopFileWatcher()
    {
        _fsDebounceTimer.Stop();
        lock (_fsLock)
        {
            _pendingChangedPaths.Clear();
            _pendingRenames.Clear();
        }

        if (_fileWatcher != null)
        {
            try
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Created -= OnFileSystemChanged;
                _fileWatcher.Deleted -= OnFileSystemChanged;
                _fileWatcher.Renamed -= OnFileSystemRenamed;
                _fileWatcher.Error -= OnFileSystemError;
                _fileWatcher.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при остановке FileSystemWatcher: {ex.Message}");
            }
            finally
            {
                _fileWatcher = null;
            }
        }
    }

    private static bool IsIgnoredPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) return true;

        if (name.StartsWith('.')) return true;
        if (name.EndsWith('~') || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;

        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith('.')) return true;
        }

        return false;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (IsIgnoredPath(e.FullPath)) return;

        lock (_fsLock)
        {
            _pendingChangedPaths.Add(e.FullPath);
        }

        Dispatcher.UIThread.Post(() =>
        {
            _fsDebounceTimer.Stop();
            _fsDebounceTimer.Start();
        }, DispatcherPriority.Background);
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        bool oldIgnored = IsIgnoredPath(e.OldFullPath);
        bool newIgnored = IsIgnoredPath(e.FullPath);

        if (oldIgnored && newIgnored) return;

        lock (_fsLock)
        {
            if (!oldIgnored && !newIgnored)
            {
                _pendingRenames[e.OldFullPath] = e.FullPath;
            }
            if (!oldIgnored) _pendingChangedPaths.Add(e.OldFullPath);
            if (!newIgnored) _pendingChangedPaths.Add(e.FullPath);
        }

        Dispatcher.UIThread.Post(() =>
        {
            _fsDebounceTimer.Stop();
            _fsDebounceTimer.Start();
        }, DispatcherPriority.Background);
    }

    private void OnFileSystemError(object sender, ErrorEventArgs e)
    {
        Debug.WriteLine($"FileSystemWatcher error: {e.GetException()?.Message}");
        if (!string.IsNullOrEmpty(_projectRootDir))
        {
            lock (_fsLock)
            {
                _pendingChangedPaths.Add(_projectRootDir);
            }
            Dispatcher.UIThread.Post(() =>
            {
                _fsDebounceTimer.Stop();
                _fsDebounceTimer.Start();
            }, DispatcherPriority.Background);
        }
    }

    private void OnFsDebounceTimerTick(object? sender, EventArgs e)
    {
        _fsDebounceTimer.Stop();

        if (_projectRootDirectory == null || string.IsNullOrWhiteSpace(_projectRootDir))
            return;

        List<string> pathsToProcess;
        Dictionary<string, string> renamesToProcess;
        lock (_fsLock)
        {
            pathsToProcess = _pendingChangedPaths.ToList();
            _pendingChangedPaths.Clear();
            renamesToProcess = new Dictionary<string, string>(_pendingRenames, PathComparer);
            _pendingRenames.Clear();
        }

        bool treeChanged = false;

        // 1. Сначала обрабатываем переименования
        if (renamesToProcess.Count > 0)
        {
            if (ProcessPendingRenames(renamesToProcess))
            {
                treeChanged = true;
            }
        }

        if (pathsToProcess.Count == 0 && !treeChanged) return;

        // 2. Находим директории, которые требуют синхронизации
        var affectedDirs = new HashSet<DirectoryVM>();

        foreach (var path in pathsToProcess)
        {
            var node = FindNodeByPath(_projectRootDirectory, path);
            if (node is DirectoryVM dirNode)
            {
                affectedDirs.Add(dirNode);
                if (dirNode.Parent != null)
                    affectedDirs.Add(dirNode.Parent);
            }
            else if (node is FileVM fileNode)
            {
                if (fileNode.Parent != null)
                    affectedDirs.Add(fileNode.Parent);
                else
                    affectedDirs.Add(_projectRootDirectory);
            }
            else
            {
                var parentDirPath = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parentDirPath))
                {
                    var parentDirVM = FindDirectoryByPath(_projectRootDirectory, parentDirPath);
                    if (parentDirVM != null)
                    {
                        affectedDirs.Add(parentDirVM);
                    }
                    else if (path.StartsWith(_projectRootDir, PathComparison))
                    {
                        affectedDirs.Add(_projectRootDirectory);
                    }
                }
                else
                {
                    affectedDirs.Add(_projectRootDirectory);
                }
            }
        }

        var sortedDirs = affectedDirs
            .OrderBy(d => d.FilePath.Length)
            .ToList();

        foreach (var dir in sortedDirs)
        {
            if (SyncDirectory(dir))
            {
                treeChanged = true;
            }
        }

        if (treeChanged)
        {
            NotifyFolderExpandedChanged();

            if (CurrentBoard != null)
            {
                ReloadCurrentProject();
            }
        }
    }

    private bool ProcessPendingRenames(Dictionary<string, string> renames)
    {
        if (_projectRootDirectory == null) return false;
        bool changed = false;

        foreach (var (oldPath, newPath) in renames)
        {
            var node = FindNodeByPath(_projectRootDirectory, oldPath);
            if (node == null) continue;

            if (node is FileVM file)
            {
                string oldRel = GetRelativePath(oldPath);
                string newRel = GetRelativePath(newPath);

                if (file is LayerVM layer)
                {
                    _projectLayers.Remove(oldRel);
                    _projectLayers[newRel] = layer;
                }
                else if (file is StylesFileVM styles)
                {
                    _projectStyles.Remove(oldRel);
                    _projectStyles[newRel] = styles;
                }
                else if (file is BoardVM && CurrentBoard == file)
                {
                    // Путь к плате обновился
                }

                file.FilePath = newPath;
                file.UpdateBreadcrumbs();

                if (file.Parent != null)
                {
                    file.Parent.Children.Remove(file);
                    InsertChildSorted(file.Parent.Children, file);
                }

                changed = true;
            }
            else if (node is DirectoryVM dir)
            {
                dir.UpdatePathRecursive(oldPath, newPath);
                dir.FilePath = newPath;

                UpdateRegisteredPathsAfterDirRename(oldPath, newPath);
                UpdateBreadcrumbsRecursive(dir);

                if (dir.Parent != null)
                {
                    dir.Parent.Children.Remove(dir);
                    InsertChildSorted(dir.Parent.Children, dir);
                }

                changed = true;
            }
        }

        return changed;
    }

    private void UpdateRegisteredPathsAfterDirRename(string oldDirPath, string newDirPath)
    {
        string oldRelDir = GetRelativePath(oldDirPath).TrimEnd('/', '\\');
        string newRelDir = GetRelativePath(newDirPath).TrimEnd('/', '\\');

        var layersToUpdate = _projectLayers
            .Where(kv => kv.Key.StartsWith(oldRelDir + "/", StringComparison.OrdinalIgnoreCase) ||
                         kv.Key.StartsWith(oldRelDir + "\\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var (oldRel, layer) in layersToUpdate)
        {
            _projectLayers.Remove(oldRel);
            string suffix = oldRel.Substring(oldRelDir.Length);
            string newRel = newRelDir + suffix;
            _projectLayers[newRel] = layer;
        }

        var stylesToUpdate = _projectStyles
            .Where(kv => kv.Key.StartsWith(oldRelDir + "/", StringComparison.OrdinalIgnoreCase) ||
                         kv.Key.StartsWith(oldRelDir + "\\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var (oldRel, style) in stylesToUpdate)
        {
            _projectStyles.Remove(oldRel);
            string suffix = oldRel.Substring(oldRelDir.Length);
            string newRel = newRelDir + suffix;
            _projectStyles[newRel] = style;
        }
    }

    private static void UpdateBreadcrumbsRecursive(DirectoryVM dir)
    {
        foreach (var child in dir.Children)
        {
            if (child is FileVM file)
            {
                file.UpdateBreadcrumbs();
            }
            else if (child is DirectoryVM subDir)
            {
                UpdateBreadcrumbsRecursive(subDir);
            }
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static FileStructVM? FindNodeByPath(DirectoryVM root, string path)
    {
        string normPath = NormalizePath(path);
        if (string.Equals(NormalizePath(root.FilePath), normPath, PathComparison))
            return root;

        foreach (var child in root.Children)
        {
            if (string.Equals(NormalizePath(child.FilePath), normPath, PathComparison))
                return child;

            if (child is DirectoryVM dir)
            {
                var found = FindNodeByPath(dir, path);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static DirectoryVM? FindDirectoryByPath(DirectoryVM root, string path)
    {
        string normPath = NormalizePath(path);
        if (string.Equals(NormalizePath(root.FilePath), normPath, PathComparison))
            return root;

        foreach (var child in root.Children)
        {
            if (child is DirectoryVM dir)
            {
                if (string.Equals(NormalizePath(dir.FilePath), normPath, PathComparison))
                    return dir;

                var found = FindDirectoryByPath(dir, path);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static void InsertChildSorted(ObservableCollection<FileStructVM> children, FileStructVM item)
    {
        bool isDir = item is DirectoryVM;

        for (int i = 0; i < children.Count; i++)
        {
            var current = children[i];
            bool currentIsDir = current is DirectoryVM;

            if (isDir)
            {
                if (!currentIsDir)
                {
                    children.Insert(i, item);
                    return;
                }

                if (string.Compare(item.Header, current.Header, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    children.Insert(i, item);
                    return;
                }
            }
            else
            {
                if (currentIsDir)
                {
                    continue;
                }

                if (string.Compare(item.Header, current.Header, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    children.Insert(i, item);
                    return;
                }
            }
        }

        children.Add(item);
    }

    private void CleanupRemovedNode(FileStructVM node)
    {
        if (node is DirectoryVM dir)
        {
            foreach (var child in dir.Children.ToList())
            {
                CleanupRemovedNode(child);
            }
        }
        else if (node is FileVM file)
        {
            string relPath = GetRelativePath(file.FilePath);

            if (file is LayerVM)
            {
                _projectLayers.Remove(relPath);
            }
            else if (file is StylesFileVM)
            {
                _projectStyles.Remove(relPath);
            }
            else if (file is BoardVM && CurrentBoard == file)
            {
                CurrentBoard = null;
            }

            if (ProjectErrors.Contains(file))
            {
                ProjectErrors.Remove(file);
                OnPropertyChanged(nameof(TotalErrorCount));
            }
        }
    }

    private bool SyncDirectory(DirectoryVM dir)
    {
        if (!Directory.Exists(dir.FilePath))
        {
            if (dir == _projectRootDirectory)
            {
                CloseProject();
                return true;
            }

            if (dir.Parent != null)
            {
                dir.Parent.Children.Remove(dir);
                CleanupRemovedNode(dir);
                return true;
            }

            return false;
        }

        string[] actualDirPaths;
        string[] actualFilePaths;

        try
        {
            actualDirPaths = Directory.GetDirectories(dir.FilePath)
                .Where(d => !IsIgnoredPath(d))
                .ToArray();
            actualFilePaths = Directory.GetFiles(dir.FilePath)
                .Where(f => !IsIgnoredPath(f))
                .ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка доступа к директории {dir.FilePath}: {ex.Message}");
            return false;
        }

        bool changed = false;

        // 1. Проверяем удаленные поддиректории
        var removedDirs = dir.Children
            .OfType<DirectoryVM>()
            .Where(existingDir => !actualDirPaths.Any(p => string.Equals(NormalizePath(p), NormalizePath(existingDir.FilePath), PathComparison)))
            .ToList();

        foreach (var removedDir in removedDirs)
        {
            dir.Children.Remove(removedDir);
            CleanupRemovedNode(removedDir);
            changed = true;
        }

        // 2. Проверяем удаленные файлы
        var removedFiles = dir.Children
            .OfType<FileVM>()
            .Where(existingFile => !actualFilePaths.Any(p => string.Equals(NormalizePath(p), NormalizePath(existingFile.FilePath), PathComparison)))
            .ToList();

        foreach (var removedFile in removedFiles)
        {
            dir.Children.Remove(removedFile);
            CleanupRemovedNode(removedFile);
            changed = true;
        }

        // 3. Проверяем новые поддиректории
        foreach (var dirPath in actualDirPaths)
        {
            bool exists = dir.Children
                .OfType<DirectoryVM>()
                .Any(existingDir => string.Equals(NormalizePath(existingDir.FilePath), NormalizePath(dirPath), PathComparison));

            if (!exists)
            {
                var newDir = LoadDirectories(dirPath, dir);
                InsertChildSorted(dir.Children, newDir);
                changed = true;
            }
        }

        // 4. Проверяем новые файлы
        foreach (var filePath in actualFilePaths)
        {
            bool exists = dir.Children
                .OfType<FileVM>()
                .Any(existingFile => string.Equals(NormalizePath(existingFile.FilePath), NormalizePath(filePath), PathComparison));

            if (!exists)
            {
                var newFile = LoadFile(filePath, dir);
                if (newFile != null)
                {
                    InsertChildSorted(dir.Children, newFile);
                    changed = true;
                }
            }
        }

        return changed;
    }
}