using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record NetDto : BoardEntityDto
{
    [YamlMember(Alias = "name")]
    public string? Name { get; init; }

    [YamlMember(Alias = "nodes")] 
    public List<EndpointDto?>? Nodes { get; init; } = [];
}