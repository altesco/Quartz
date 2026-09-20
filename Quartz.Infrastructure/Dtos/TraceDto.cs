using System.ComponentModel.DataAnnotations;
using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record TraceDto : BoardDimensionalObjectDto
{
    [Required]
    [YamlMember(Alias = "net")] 
    public string? Net { get; init; } // Имя цепи (GND, VCC)

    [Required]
    [YamlMember(Alias = "from")] 
    public EndpointBaseDto? From { get; init; }

    [Required]
    [YamlMember(Alias = "to")] 
    public EndpointBaseDto? To { get; init; }

    [YamlMember(Alias = "width")] 
    public double? Width { get; init; } = 0.25; // Ширина дорожки

    [YamlMember(Alias = "coord-mode")] 
    public CoordinateMode CoordMode { get; init; } = CoordinateMode.Relative;

    [YamlMember(Alias = "points")] 
    public List<Point2DDto>? MiddlePoints { get; init; } = [];
}