using System.Collections.Frozen;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Models;

public class BoardModel
{
    public IReadOnlyDictionary<string, Net> Nets { get; init; } = FrozenDictionary<string, Net>.Empty;
    public IReadOnlyDictionary<string, Via> Vias { get; init; } = FrozenDictionary<string, Via>.Empty;

    public Dictionary<string, LayerModel> Layers { get; init; } = new();//FrozenDictionary<string, LayerModel>.Empty;

    // Предрассчитанный кеш ближайших Via: O(1) доступ при рендере!
    public Dictionary<NodeViaKey, Via> NearestVias { get; init; } = new();

    public Dictionary<string, Component> GetAllComponents()
    {
        return Layers.Values
            .SelectMany(l => l.Components)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    } 
}