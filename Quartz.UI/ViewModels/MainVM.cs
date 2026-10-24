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

    public int TotalErrorCount => ProjectErrors.Sum(f => f.Errors.Count);

    private readonly IBoardProcessingCoordinator _boardProcessingCoordinator;
    private readonly IFolderDialogService _folderDialogService;

    private string? _projectRootDir;
    public string? ProjectRootDir => _projectRootDir;

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

        ProjectErrors.CollectionChanged += OnProjectErrorsCollectionChanged;

        _projectRootDir = Path.Combine("/", "home", "alexandr", "Games", "Проект");
        if (Directory.Exists(_projectRootDir))
        {
            LoadProjectFromDirectory(_projectRootDir);
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
        if (parameter is EditorError error)
        {
            var file = ProjectErrors.FirstOrDefault(f => f.Errors.Contains(error));
            if (file != null)
            {
                NavigateToError(file, error);
            }
        }
        else if (parameter is FileVM file)
        {
            OpenFileTab(file);
        }
    }

    public void NavigateToError(FileVM file, EditorError error)
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
            editor.RequestNavigateTo(error.Line, error.Column);
            RecordNavigationPoint(file, (int)error.Line, (int)error.Column);
        }
    }

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
            }
        }
        if (e.OldItems != null)
        {
            foreach (FileVM file in e.OldItems.OfType<FileVM>())
            {
                file.Errors.CollectionChanged -= OnFileErrorsCollectionChanged;
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
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(SidebarHeaderTitle));
        _projectRootDirectory = LoadDirectories(_projectRootDir, parent: null);
        OnPropertyChanged(nameof(FileTree));

        // 3. Если в проекте есть плата, запускаем расчет
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
        if (fileVM.Errors.Count == 0)
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
}