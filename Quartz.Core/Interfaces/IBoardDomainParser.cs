using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.Core.Interfaces;

public interface IBoardDomainParser
{
    BoardModel? ParseBoard(
        string yamlText,
        Dictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, LayerModel>? layersMap,
        IReadOnlyCollection<string>? availableLayerPaths,
        IReadOnlyCollection<string>? availableStylePaths,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors);

    void ValidateInterlayerNetsVia(BoardModel board, List<EditorError> errors);
}