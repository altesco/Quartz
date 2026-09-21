namespace Quartz.Core.Models.BoardEntities;

public abstract class Connection : BoardEntity
{
    public string Name { get; set; } = "";

    // Координата пина
    public Point2D Point { get; set; }

    // Спецификация печатных плат (Footprint Pad Properties)
    public Shape Shape { get; set; } = new RectShape { Height = 6, Width = 6, CornerRadius = 3 };
}