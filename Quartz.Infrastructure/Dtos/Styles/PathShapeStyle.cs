using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record PathShapeStyle : Style
{
    [YamlMember(Alias = "start-point")] 
    public Point2DDto StartPoint { get; init; } = new();

    [YamlMember(Alias = "segments")] 
    public List<SegmentDto?>? Segments { get; init; }
}