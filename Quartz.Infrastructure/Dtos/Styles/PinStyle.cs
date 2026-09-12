using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record PinStyle : Style
{
    // Режим задания координат (Относительно центра компонента или Абсолютные координаты платы)
    [YamlMember(Alias = "coord-mode")] 
    public CoordinateMode CoordMode { get; init; } = CoordinateMode.Relative;

    // Спецификация печатных плат (Footprint Pad Properties)
    [YamlMember(Alias = "shape")] 
    public ShapeDto? Shape { get; init; }

    [YamlMember(Alias = "drill-diameter")] 
    public double DrillDiameter { get; init; } = 0.8; // 0 для чисто SMD

    [YamlMember(Alias = "is-plated")] 
    public bool IsPlated { get; init; } = true; // Металлизация отверстия

    [YamlMember(Alias = "electrical-type")]
    public PinElectricalType ElectricalType { get; init; } = PinElectricalType.Passive;
}