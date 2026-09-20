using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Interfaces;

public interface ILayerDomainParser
{
    LayerModel? Parse(
        string yamlText, 
        Dictionary<string, Component> componentsMap, 
        IReadOnlyDictionary<string, Net> netsMap, 
        IReadOnlyDictionary<string, Via>? viasMap,
        out List<EditorError> errors);

    Dictionary<string, Component> ParseComponents(string yamlText, out List<EditorError> errors);

    string? ExtractLayerName(string yamlText);
}