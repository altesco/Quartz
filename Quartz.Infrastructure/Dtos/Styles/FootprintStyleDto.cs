using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record FootprintStyleDto : StyleDto
{
    [YamlMember(Alias = "pads")]
    public List<PadDto?>? Pads { get; init; }

    [YamlMember(Alias = "shape")]
    public ShapeDto? Shape { get; init; } 

    [YamlMember(Alias = "unit")] 
    public LengthUnit? Unit { get; init; }
}