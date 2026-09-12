using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public abstract record Style : BoardEntityDto
{
    [YamlMember(Alias = "name")] 
    public string? Name { get; init; }
}