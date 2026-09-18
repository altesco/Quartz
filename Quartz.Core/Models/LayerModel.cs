using System.Collections.Frozen;
using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Models;

public class LayerModel
{
    public Shape Shape { get; set; } = new RectShape();

    public LengthUnit Unit { get; set; } = LengthUnit.Mm;

    public IReadOnlyDictionary<string, Component> Components { get; init; } = FrozenDictionary<string, Component>.Empty;
    public List<Trace> Traces { get; set; } = [];

    public IReadOnlyDictionary<string, Via> Vias { get; init; } = FrozenDictionary<string, Via>.Empty;

    public string Name { get; set; } = "";
}