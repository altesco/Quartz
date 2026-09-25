using System.IO;
using System.Threading.Tasks;
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
    public override Extension Extension => Extension.Pcby;

    protected override async Task ProcessDocument(string text)
    {
        Errors.Clear();

        // 1. Быстро забираем тексты из словаря MainVM (без обхода деревьев!)
        var stylesTexts = MainVM.GetAllStylesTexts();
        var layerTexts = MainVM.GetAllLayerTexts();

        // 2. Считаем проект
        var (boardResult, layerResults, stylesResults) = await Task.Run(() =>
            _coordinator.ProcessProject(text, layerTexts, stylesTexts)
        );

        // 3. Обновляем ошибки и модель самой платы
        foreach (var err in boardResult.Errors)
        {
            Errors.Add(err);
        }

        if (boardResult.Model != null)
        {
            BoardModel = boardResult.Model;
        }

        // 4. Отдаем результат в MainVM для рассылки по слоям
        MainVM.NotifyStylesUpdated(stylesResults);
        MainVM.NotifyLayersBoardUpdated(boardResult.Primitives, layerResults);
    }

    public async Task ProcessBoardProject()
    {
        string text = Document != null && !string.IsNullOrWhiteSpace(Document.Text)
            ? Document.Text
            : File.ReadAllText(FilePath);

        await ProcessDocument(text);
    }
}