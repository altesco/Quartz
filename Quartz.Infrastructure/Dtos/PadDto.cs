using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record PadDto : ConnectionDto
{
    [YamlMember(Alias = "drill-diameter")] 
    public double? DrillDiameter { get; init; }

    [YamlMember(Alias = "is-plated")] 
    public bool? IsPlated { get; init; }

    [YamlMember(Alias = "electrical-type")]
    public PinElectricalType? ElectricalType { get; init; }
}