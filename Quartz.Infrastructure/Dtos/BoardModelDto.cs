using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record BoardModelDto
{
    [YamlMember(Alias = "layers")]
    public List<string?>? Layers { get; init; } = []; 

    [YamlMember(Alias = "nets")]
    public List<NetDto?>? Nets { get; init; } = [];

    [YamlMember(Alias = "vias")]
    public List<ViaDto?>? Vias { get; init; } = [];
}