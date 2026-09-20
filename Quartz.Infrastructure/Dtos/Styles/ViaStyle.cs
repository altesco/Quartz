using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record ViaStyle : ConnectionStyle
{
    [YamlMember(Alias = "drill-diameter")] 
    public double? DrillDiameter { get; init; }
}