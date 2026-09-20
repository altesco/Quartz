using System.IO;
using System.Linq;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public class BoardVM : EditorVM
{
    private readonly IBoardProcessingCoordinator _coordinator;

    public BoardVM(
        MainVM mainVM,
        string filePath,
        DirectoryVM? parent,
        IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
        _coordinator = coordinator;
    }

    public BoardModel? BoardModel { get; set; }

    public override Extension Extension => Extension.Brd;

    protected override void ProcessDocument(string text)
    {
        Errors.Clear();

        var layerTexts = MainVM.GetAllLayerTexts();
        var (boardResult, layerResults) = _coordinator.ProcessProject(text, layerTexts);

        foreach (var err in boardResult.Errors)
        {
            Errors.Add(err);
        }

        // Проверяем ошибки во всем проекте
        bool hasAnyErrors = boardResult.Errors.Count > 0 ||
                            layerResults.Values.Any(r => r.Errors.Count > 0);

        if (!hasAnyErrors)
        {
            BoardModel = boardResult.Model;
        }

        // Передаем примитивы, результаты слоев И флаг наличия ошибок!
        MainVM.NotifyLayersBoardUpdated(boardResult.Primitives, layerResults, hasAnyErrors);
    }

    public void ProcessBoardProject()
    {
        string text = !string.IsNullOrWhiteSpace(Document?.Text)
            ? Document.Text
            : File.ReadAllText(FilePath);

        ProcessDocument(text);
    }
}