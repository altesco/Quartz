using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public abstract record BoardStylableObjectDto : BoardEntityDto
{
    [YamlMember(Alias = "style")] 
    public string? Style { get; init; }
}