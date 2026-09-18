using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record ViaStyle : ConnectionStyle
{
    [YamlMember(Alias = "from")] 
    public string? From { get; init; }

    [YamlMember(Alias = "to")] 
    public string? To { get; init; }

    [YamlMember(Alias = "drill-diameter")] 
    public double? DrillDiameter { get; init; }
}