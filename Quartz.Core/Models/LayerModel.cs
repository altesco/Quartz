using System.Collections.Frozen;
using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Models;

public class LayerModel
{
    public Shape Shape { get; set; } = new RectShape { Height = 800, Width = 600 };

    public LengthUnit Unit { get; set; } = LengthUnit.Mm;

    public IReadOnlyDictionary<string, Component> Components { get; set; } = FrozenDictionary<string, Component>.Empty;

    public List<Trace> Traces { get; set; } = [];

    public string Name { get; set; } = "";
    public int Index { get; set; }
}