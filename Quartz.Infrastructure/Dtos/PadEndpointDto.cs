using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record PadEndpointDto : EndpointBaseDto
{
    [YamlMember(Alias = "comp")]
    public string? Comp { get; init; }

    [YamlMember(Alias = "pad")]
    public string? Pad { get; init; } 
}