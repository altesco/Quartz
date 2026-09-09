using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record EndpointDto : BoardEntityDto
{
    [YamlMember(Alias = "comp")]
    public string? Comp { get; init; }

    [YamlMember(Alias = "pin")]
    public string? Pin { get; init; } 
}