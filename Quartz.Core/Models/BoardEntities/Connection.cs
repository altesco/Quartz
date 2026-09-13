namespace Quartz.Core.Models.BoardEntities;

public abstract class Connection : BoardEntity //BoardDimensionalObject
{
    public string Name { get; set; } = ""; // Например: "GND", "VCC", "1", "A"

    // Координата пина
    public Point2D Point { get; set; }

    // Спецификация печатных плат (Footprint Pad Properties)
    public Shape Shape { get; set; } = new RectShape();
}