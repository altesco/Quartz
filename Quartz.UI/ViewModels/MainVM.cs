using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quartz.Application.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.UI.Enums;

namespace Quartz.UI.ViewModels;

public partial class MainVM : ObservableObject
{
    [ObservableProperty] private LayoutRootVM _rootNode;

    [ObservableProperty] private bool _isTabReplacing;
    [ObservableProperty] private bool _isFileReplacing;

    public PanelVM? SourcePanel { get; set; }
    [ObservableProperty] private FileVM? _draggedTab;
    public FileStructVM? TappedFile { get; set; }
    [ObservableProperty] private FileStructVM? _draggedFile;

    [ObservableProperty] private PanelVM _activePanel;

    public bool IsReadyToDrag { get; set; }
    public double DragStartX { get; set; }
    public double DragStartY { get; set; }

    [ObservableProperty] private double _fakeFileX;
    [ObservableProperty] private double _fakeFileY;

    private readonly IBoardProcessingCoordinator _boardProcessingCoordinator;

    public MainVM(IBoardProcessingCoordinator boardProcessingCoordinator)
    {
        _boardProcessingCoordinator = boardProcessingCoordinator;

        _rootNode = new PanelVM(mainVM: this)
        {
            Size = "1*"
        };

        _activePanel = (PanelVM)_rootNode;

        FileTree.Add(LoadDirectories(
            Path.Combine("/", "home", "alexandr", "Games", "Проект"),
            parent: null
        ));
    }

    public ObservableCollection<FileStructVM> FileTree { get; } = [];

    public BoardVM? CurrentBoard { get; set; }

    /// <summary>
    /// Разделяет целевую панель на две, вставляя между ними сплиттер.
    /// </summary>
    public void SplitPanel(PanelVM targetPanel, PanelVM newPanel, Side side)
    {
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
        if (RootNode == emptyPanel)
            return;

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

    /// <summary>
    /// Публичный метод для запуска сброса подсветок со всей разметки
    /// </summary>
    public void ResetAllPanelSides() => ResetSidesRecursive(RootNode);

    /// <summary>
    /// Рекурсивный обход дерева панелей для обнуления NewNodeSide
    /// </summary>
    private void ResetSidesRecursive(LayoutRootVM node)
    {
        if (node is PanelVM panel)
        {
            panel.NewNodeSide = Side.None;
            // Очищаем синие полосочки у ВСЕХ вкладок этой панели
            foreach (var tab in panel.Tabs)
            {
                tab.InsertSide = Side.None;
            }
        }
        else if (node is SplitContainerVM container)
        {
            foreach (var child in container.Children)
            {
                ResetSidesRecursive(child);
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
                if (Path.GetExtension(f) is ".brd" or ".lyr" or ".sch")
                {
                    var file = LoadFile(f, dir);
                    if (file != null)
                        dir.Children.Add(file);
                }
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
        switch (ext)
        {
            case ".brd":
                var board = new BoardVM(mainVM: this, parent: parent, filePath: path,
                    coordinator: _boardProcessingCoordinator)
                {
                    FilePath = path
                };
                CurrentBoard = board;
                return board;
            case ".lyr":
                var layer = new LayerVM(mainVM: this, parent: parent, filePath: path,
                    coordinator: _boardProcessingCoordinator)
                {
                    FilePath = path
                };
                return layer;
        }

        return null;
    }

    [RelayCommand]
    private void MoveFile(PointerEventArgs? e)
    {
        if (e is null)
            return;

        // апдейт координат для отрисовки перемещаемого файла
        var currentPos = e.GetPosition(null);
        FakeFileX = currentPos.X;
        FakeFileY = currentPos.Y;

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

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>();
        if (treeViewItem?.DataContext is not FileStructVM dc)
            return;

        dc.TapStartCommand.Execute(e);
    }

    [RelayCommand]
    private void FileReplaced(PointerReleasedEventArgs? e)
    {
        if (e?.Source is not Visual visualSource)
            return;

        var treeViewItem = visualSource.FindAncestorOfType<TreeViewItem>();
        if (treeViewItem?.DataContext is not FileStructVM dc)
            return;

        dc.TapEndCommand.Execute(null);
    }


    // --- МЕТОДЫ ДЛЯ СВЯЗИ ПЛАТЫ И СЛОЕВ ---

    /// <summary>
    /// Собирает тексты всех слоев (.lyr) проекта.
    /// Приоритет отдается несохраненным изменениям из открытых вкладок.
    /// </summary>
    public Dictionary<string, string> GetAllLayerTexts()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Собираем открытые в редакторе слои (у них самый свежий текст из Document.Text)
        var openLayers = GetOpenLayersRecursive(RootNode)
            .ToDictionary(
                l => GetRelativePath(l.FilePath),
                l => l,
                StringComparer.OrdinalIgnoreCase);

        // 2. Обходим дерево файлов и собираем тексты всех .lyr
        CollectLayersFromTreeRecursive(FileTree, result, openLayers);

        return result;
    }

    public void ReloadCurrentProject()
    {
        if (CurrentBoard is { } boardVm)
        {
            // Запускаем принудительную пересборку платы, 
            // даже если файл .brd не открыт во вкладке редактора!
            boardVm.ProcessBoardProject();
        }
    }

    /// <summary>
    /// Уведомляет все открытые вкладки слоев об обновлении платы, 
    /// передавая им уже просчитанные результаты или принуждая к перерисовке.
    /// </summary>
    public void NotifyLayersBoardUpdated(
        List<DrawingPrimitive> boardOverlayPrimitives,
        IDictionary<string, ProcessResult<LayerModel>> layerResults,
        bool hasAnyErrors)
    {
        foreach (var layerVM in GetOpenLayersRecursive(RootNode))
        {
            string relPath = GetRelativePath(layerVM.FilePath);

            if (layerResults.TryGetValue(relPath, out var layerResult))
            {
                // 1. Ошибки слоя обновляем ВСЕГДА, чтобы редактор кода закрашивал проблемные строки
                layerVM.Errors.Clear();
                foreach (var err in layerResult.Errors)
                {
                    layerVM.Errors.Add(err);
                }

                // 2. А вот модель и рендер-данные меняем ТОЛЬКО если во всем проекте НЕТ ошибок!
                if (!hasAnyErrors)
                {
                    if (layerResult.Model != null)
                    {
                        layerVM.LayerModel = layerResult.Model;
                    }

                    if (layerVM.Canvas != null)
                    {
                        // Склеиваем примитивы текущего слоя и оверлей платы (Vias + Nets)
                        layerVM.Canvas.RenderData = layerResult.Primitives
                            .Concat(boardOverlayPrimitives)
                            .ToList();
                    }
                }
            }
        }
    }

// --- ВСПОМОГАТЕЛЬНЫЕ ПРИВАТНЫЕ МЕТОДЫ ---

    private IEnumerable<LayerVM> GetOpenLayersRecursive(LayoutRootVM node)
    {
        if (node is PanelVM panel)
        {
            foreach (var tab in panel.Tabs.OfType<LayerVM>())
            {
                yield return tab;
            }
        }
        else if (node is SplitContainerVM container)
        {
            foreach (var child in container.Children)
            {
                foreach (var layer in GetOpenLayersRecursive(child))
                {
                    yield return layer;
                }
            }
        }
    }

    private void CollectLayersFromTreeRecursive(
        IEnumerable<FileStructVM> items,
        Dictionary<string, string> result,
        Dictionary<string, LayerVM> openLayers)
    {
        foreach (var item in items)
        {
            if (item is DirectoryVM dir)
            {
                CollectLayersFromTreeRecursive(dir.Children, result, openLayers);
            }
            else if (item is LayerVM layer)
            {
                string relPath = GetRelativePath(layer.FilePath);

                // Если слой открыт в редакторе — берём не сохранённый текст из вкладки
                if (openLayers.TryGetValue(relPath, out var openLayerVm))
                {
                    result[relPath] = openLayerVm.Document?.Text ?? string.Empty;
                }
                else
                {
                    // Иначе читаем с диска
                    try
                    {
                        result[relPath] = File.ReadAllText(layer.FilePath);
                    }
                    catch
                    {
                        result[relPath] = string.Empty;
                    }
                }
            }
        }
    }

    private string GetRelativePath(string fullPath)
    {
        var rootDir = FileTree.FirstOrDefault()?.FilePath;
        if (string.IsNullOrEmpty(rootDir))
            return Path.GetFileName(fullPath);

        // Нормализуем слеши к UNIX-стилю для YAML
        return Path.GetRelativePath(rootDir, fullPath).Replace('\\', '/');
    }
}