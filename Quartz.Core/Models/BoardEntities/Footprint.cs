using System.Collections.Frozen;

namespace Quartz.Core.Models.BoardEntities;

public class Footprint : BoardEntity //BoardDimensionalObject
{
    public IReadOnlyDictionary<string, Pad> Pads { get; set; } = FrozenDictionary<string, Pad>.Empty;
    public Shape Shape { get; set; } = new RectShape { Width = 55, Height = 30 };
}