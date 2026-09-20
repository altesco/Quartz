using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record NetDto : BoardEntityDto
{
    [Required]
    [YamlMember(Alias = "name")]
    public string? Name { get; init; }

    [YamlMember(Alias = "nodes")] 
    public List<PadEndpointDto?>? Nodes { get; init; } = [];
}