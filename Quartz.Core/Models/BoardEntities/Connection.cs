namespace Quartz.Core.Models.BoardEntities;

public abstract class Connection : BoardEntity
{
    public string Name { get; set; } = "";

    public Point2D Point { get; set; }

    public Shape Shape { get; set; } = new RectShape { Height = 6, Width = 6, CornerRadius = 3 };

    public abstract override Connection Clone();
}