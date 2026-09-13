using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public abstract record ComponentStyle : Style
{
    [YamlMember(Alias = "value")] 
    public double? Value { get; init; }

    [YamlMember(Alias = "shape")] 
    public ShapeDto? Shape { get; init; }

    [YamlMember(Alias = "angle")] 
    public double? Angle { get; init; }

    [YamlMember(Alias = "footprint")] 
    public FootprintDto? Footprint { get; init; }

    [YamlMember(Alias = "mounting-type")] 
    public MountingType? MountingType { get; init; }

    [YamlMember(Alias = "name-settings")] 
    public NameSettingsDto? NameSettings { get; init; }

    [YamlMember(Alias = "unit")] 
    public LengthUnit? Unit { get; init; }

    [YamlMember(Alias = "pins")] 
    public List<PinDto?>? Pins { get; init; }
}