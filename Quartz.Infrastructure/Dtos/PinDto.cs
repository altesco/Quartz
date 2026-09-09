using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record PinDto : BoardEntityDto
{
    [YamlMember(Alias = "name")] 
    public string? Name { get; init; } = string.Empty; // Например: "GND", "VCC", "1", "A"

    // Координата пина
    [YamlMember(Alias = "point")] 
    public Point2DDto Point { get; init; }

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