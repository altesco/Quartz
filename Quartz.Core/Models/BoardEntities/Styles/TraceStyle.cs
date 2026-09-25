using Quartz.Core.Enums;


namespace Quartz.Core.Models.BoardEntities.Styles;

public class TraceStyle : Style
{
    public double? Width { get; set; }
    public LengthUnit? Unit { get; set; }
}