using System.Collections.Frozen;

namespace Quartz.Core.Models.BoardEntities;

public class Footprint : BoardEntity
{
    public IReadOnlyDictionary<string, Pad> Pads { get; set; } = FrozenDictionary<string, Pad>.Empty;
    public Shape Shape { get; set; } = new RectShape { Width = 55, Height = 30 };

    public override Footprint Clone()
    {
        var clone = (Footprint)MemberwiseClone();
        clone.Shape = Shape?.Clone()!;
        clone.Pads = Pads.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()).ToFrozenDictionary();
        return clone;
    }
}