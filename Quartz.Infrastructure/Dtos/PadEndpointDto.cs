using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record PadEndpointDto : EndpointBaseDto
{
    [Required]
    [YamlMember(Alias = "comp")]
    public string? Comp { get; init; }

    [Required]
    [YamlMember(Alias = "pad")]
    public string? Pad { get; init; } 
}