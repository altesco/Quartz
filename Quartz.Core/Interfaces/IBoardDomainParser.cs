using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Interfaces;

public interface IBoardDomainParser
{
    List<string> ExtractLayerPaths(string yamlText);

    BoardModel? Parse(
        string yamlText,
        Dictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, LayerModel>? layersMap,
        IReadOnlyCollection<string>? availableLayerPaths,
        out List<EditorError> errors);
}