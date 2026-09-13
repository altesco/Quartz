using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record PadStyle : ConnectionStyle
{
    [YamlMember(Alias = "drill-diameter")] 
    public double? DrillDiameter { get; init; }

    [YamlMember(Alias = "is-plated")] 
    public bool? IsPlated { get; init; }

    [YamlMember(Alias = "electrical-type")]
    public PinElectricalType? ElectricalType { get; init; }
}