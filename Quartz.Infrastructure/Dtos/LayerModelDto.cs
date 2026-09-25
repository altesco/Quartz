using System.ComponentModel.DataAnnotations;
using Quartz.Core.Enums;
using Quartz.Infrastructure.Dtos.Styles;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record LayerModelDto
{
    [YamlMember(Alias = "shape")] 
    public ShapeDto? Shape { get; set; }

    [YamlMember(Alias = "unit")]
    public LengthUnit Unit { get; set; } = LengthUnit.Mm;

    [YamlMember(Alias = "components")] 
    public List<ComponentDto?>? Components { get; set; }

    [YamlMember(Alias = "traces")] 
    public List<TraceDto?>? Traces { get; set; }

    [YamlMember(Alias = "styles")]
    public List<StyleDto?>? Styles { get; set; } = [];

    [Required]
    [YamlMember(Alias = "name")]
    public string? Name { get; init; }
}