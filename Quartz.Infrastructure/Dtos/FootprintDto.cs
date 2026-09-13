using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record FootprintDto : BoardDimensionalObjectDto
{
    [YamlMember(Alias = "pads")]
    public List<PadDto?>? Pads { get; init; }

    [YamlMember(Alias = "shape")]
    public ShapeDto? Shape { get; init; } 
}