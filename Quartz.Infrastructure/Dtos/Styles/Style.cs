using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public abstract record Style : BoardEntityDto
{
    [Required]
    [YamlMember(Alias = "name")] 
    public string? Name { get; init; }
}