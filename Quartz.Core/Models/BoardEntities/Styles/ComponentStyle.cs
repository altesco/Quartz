using Quartz.Core.Enums;


namespace Quartz.Core.Models.BoardEntities.Styles;

public abstract class ComponentStyle : Style
{
    public double? Value { get; set; }
    public Shape? Shape { get; set; }
    public double? Angle { get; set; }
    public MountingType? MountingType { get; set; }
    public NameSettings? NameSettings { get; set; }
    public LengthUnit? Unit { get; set; }
    public List<Pin>? Pins { get; set; }
}