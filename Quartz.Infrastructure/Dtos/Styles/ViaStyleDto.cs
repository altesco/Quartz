using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record ViaStyleDto : ConnectionStyleDto
{
    [YamlMember(Alias = "drill-diameter")] 
    public double? DrillDiameter { get; init; }
}