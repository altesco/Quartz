using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record BoardModelDto
{
    [YamlMember(Alias = "nets")]
    public List<NetDto?>? Nets { get; set; } = [];
}