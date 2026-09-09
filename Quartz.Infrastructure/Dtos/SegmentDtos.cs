using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record SegmentDto : BoardEntityDto
{
    [YamlMember(Alias = "point")] 
    public Point2DDto Point { get; init; }
}

public record LineSegmentDto : SegmentDto;

public record ArcSegmentDto : SegmentDto
{
    [YamlMember(Alias = "radius")] 
    public double Radius { get; init; }

    [YamlMember(Alias = "clockwise")] 
    public bool IsClockwise { get; init; }

    [YamlMember(Alias = "large-arc")] 
    public bool IsLargeArc { get; init; }
}