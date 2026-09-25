using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.Core.Interfaces;

public interface ILayerDomainParser
{
    LayerModel? ParseLayer(
        string yamlText, 
        Dictionary<string, Component> componentsMap, 
        IReadOnlyDictionary<string, Net> netsMap, 
        IReadOnlyDictionary<string, Via>? viasMap,
        IReadOnlyDictionary<string, Style>? stylesMap,
        out List<EditorError> errors);

    Dictionary<string, Component> ParseComponents(string yamlText, out List<EditorError> errors);

    string? ExtractLayerName(string yamlText);
}