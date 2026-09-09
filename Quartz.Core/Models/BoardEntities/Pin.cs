using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public class Pin : BoardEntity
{
    public string Name { get; set; } = ""; // Например: "GND", "VCC", "1", "A"

    // Координата пина
    public Point2D Point { get; set; }

    // Режим задания координат (Относительно центра компонента или Абсолютные координаты платы)
    public CoordinateMode CoordMode { get; set; } = CoordinateMode.Relative;

    // Спецификация печатных плат (Footprint Pad Properties)
    public Shape Shape { get; set; } = new RectShape();

    public double DrillDiameter { get; set; } = 0.8; // 0 для чисто SMD
    public bool IsPlated { get; set; } = true; // Металлизация отверстия

    public PinElectricalType ElectricalType { get; set; } = PinElectricalType.Passive;
}