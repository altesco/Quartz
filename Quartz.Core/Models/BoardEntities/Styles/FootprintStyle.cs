using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities.Styles;

public class FootprintStyle : Style
{
    public List<Pad>? Pads { get; set; }
    public Shape? Shape { get; set; }
    public LengthUnit? Unit { get; set; }

    public override FootprintStyle Clone()
    {
        var clone = (FootprintStyle)MemberwiseClone();
        clone.Shape = Shape?.Clone();
        clone.Pads = Pads?.Select(p => p.Clone()).ToList();
        return clone;
    }
}