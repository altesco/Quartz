using System.ComponentModel.DataAnnotations;
using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public abstract record ComponentDto : BoardDimensionalObjectDto
{
    [Required]
    [YamlMember(Alias = "name")] 
    public string? Name { get; init; } 

    [YamlMember(Alias = "value")] 
    public double? Value { get; init; }

    [YamlMember(Alias = "point")] 
    public Point2DDto? Point { get; init; }

    [YamlMember(Alias = "shape")]
    public ShapeDto? Shape { get; init; }

    [YamlMember(Alias = "angle")] 
    public double? Angle { get; init; }

    [Required]
    [YamlMember(Alias = "footprint")] 
    public FootprintDto? Footprint { get; init; }

    [YamlMember(Alias = "mounting-type")] 
    public MountingType? MountingType { get; init; }

    [YamlMember(Alias = "name-settings")] 
    public NameSettingsDto? NameSettings { get; init; }

    [YamlMember(Alias = "pins")] 
    public List<PinDto?>? Pins { get; init; }
}