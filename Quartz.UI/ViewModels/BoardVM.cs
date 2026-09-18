using System.IO;
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

        BoardModel = boardResult.Model;

        // Пробрасываем примитивы платы (List<DrawingPrimitive>) 
        // и словарь результатов слоев (Dictionary<string, ProcessResult<LayerModel>>)
        MainVM.NotifyLayersBoardUpdated(boardResult.Primitives, layerResults);
    }

    public void ProcessBoardProject()
    {
        // Если документ открыт в редакторе — берем его Text. 
        // Если нет — читаем свежий текст прямо с диска!
        string text = !string.IsNullOrWhiteSpace(Document?.Text)
            ? Document.Text
            : File.ReadAllText(FilePath);

        ProcessDocument(text);
    }
}