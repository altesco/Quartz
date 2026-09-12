using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record SegmentStyle : Style
{
    [YamlMember(Alias = "point")] 
    public Point2DDto? Point { get; init; }
}

public record LineSegmentStyle : SegmentStyle;

public record ArcSegmentStyle : SegmentStyle
{
    [YamlMember(Alias = "radius")] 
    public double Radius { get; init; }

    [YamlMember(Alias = "clockwise")] 
    public bool IsClockwise { get; init; }

    [YamlMember(Alias = "large-arc")] 
    public bool IsLargeArc { get; init; }
}