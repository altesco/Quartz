using Quartz.Core.Enums;


namespace Quartz.Core.Models.BoardEntities.Styles;

public class FootprintStyle : Style
{
    public List<Pad>? Pads { get; set; }
    public Shape? Shape { get; set; }
    public LengthUnit? Unit { get; set; }
}