using Quartz.Core.Enums;
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
}