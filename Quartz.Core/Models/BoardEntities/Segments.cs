namespace Quartz.Core.Models.BoardEntities;

public class Segment : BoardEntity
{
    public Point2D Point { get; set; }
}

public class LineSegment : Segment;

public class ArcSegment : Segment
{
    public double Radius { get; set; }
    public bool IsClockwise { get; set; }
    public bool IsLargeArc { get; set; }
}