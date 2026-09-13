using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record TraceDto : BoardDimensionalObjectDto
{
    [YamlMember(Alias = "net")] 
    public string NetName { get; init; } = string.Empty; // Имя цепи (GND, VCC)

    [YamlMember(Alias = "from")] 
    public EndpointDto? From { get; init; } // "(R1.1).1"

    [YamlMember(Alias = "to")] 
    public EndpointDto? To { get; init; } // "(U1).12"

    [YamlMember(Alias = "width")] 
    public double? Width { get; init; } = 0.25; // Ширина дорожки

    [YamlMember(Alias = "coord-mode")] 
    public CoordinateMode CoordMode { get; init; } = CoordinateMode.Relative;

    [YamlMember(Alias = "points")] 
    public List<Point2DDto>? MiddlePoints { get; init; } = [];
}