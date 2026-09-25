using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record PathShapeStyleDto : ShapeStyleDto
{
    [YamlMember(Alias = "start-point")] 
    public Point2DDto? StartPoint { get; init; }

    [YamlMember(Alias = "segments")] 
    public List<SegmentDto?>? Segments { get; init; }
}