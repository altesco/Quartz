using Quartz.Core.Models;

namespace Quartz.Application.Interfaces;

public interface IBoardProcessingCoordinator
{
    ProcessResult<LayerModel> Process(string text, BoardModel? boardModel);

    (ProcessResult<BoardModel> BoardResult, Dictionary<string, ProcessResult<LayerModel>> LayerResults) ProcessProject(
        string boardText, IReadOnlyDictionary<string, string> layerTexts);
}