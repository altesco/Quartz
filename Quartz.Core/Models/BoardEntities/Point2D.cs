namespace Quartz.Core.Models.BoardEntities;

public readonly record struct Point2D
{
    public Point2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; init; }
    public double Y { get; init; }
}