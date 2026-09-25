using Quartz.Core.Enums;


namespace Quartz.Core.Models.BoardEntities.Styles;

public abstract class ConnectionStyle : Style
{
    public Shape? Shape { get; set; }
    public LengthUnit? Unit { get; set; }
}