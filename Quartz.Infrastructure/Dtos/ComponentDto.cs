using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public abstract record ComponentDto : BoardDimensionalObjectDto
{
    [YamlMember(Alias = "id")] 
    public int Id { get; init; }

    [YamlMember(Alias = "type")] 
    public string? Type { get; init; } 

    [YamlMember(Alias = "name")] 
    public string? Name { get; init; } 

    [YamlMember(Alias = "value")] 
    public double Value { get; init; }

    [YamlMember(Alias = "point")] 
    public Point2DDto Point { get; init; } = new();

    [YamlMember(Alias = "shape")]
    public ShapeDto? Shape { get; init; }

    [YamlMember(Alias = "angle")] 
    public double Angle { get; init; } = 0.0;

    [YamlMember(Alias = "footprint")] 
    public string Footprint { get; init; } = string.Empty;

    [YamlMember(Alias = "mounting-type")] 
    public MountingType MountingType { get; init; } = MountingType.SMD;

    [YamlMember(Alias = "name-settings")] 
    public ComponentTextSettingsDto NameSettings { get; init; } = new();

    [YamlMember(Alias = "pins")] 
    public List<PinDto>? Pins { get; init; } = [];
}