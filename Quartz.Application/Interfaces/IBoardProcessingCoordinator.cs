using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.Application.Interfaces;

public interface IBoardProcessingCoordinator
{
    ProcessResult<LayerModel> ProcessLayer(string text, BoardModel? boardModel);

    ProjectResult ProcessProject(
        string boardText, 
        IReadOnlyDictionary<string, string> layerTexts,
        IReadOnlyDictionary<string, string> styleTexts);

    ProcessResult<List<Style>> ProcessStyles(string text);
}