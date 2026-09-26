namespace Quartz.Core.Models.BoardEntities;

public abstract class Segment : BoardEntity
{
    public Point2D Point { get; set; }

    public abstract override Segment Clone();
}

public class LineSegment : Segment
{
    public override LineSegment Clone() => (LineSegment)MemberwiseClone();
}

public class ArcSegment : Segment
{
    public double Radius { get; set; }
    public bool IsClockwise { get; set; }
    public bool IsLargeArc { get; set; }

    public override ArcSegment Clone() => (ArcSegment)MemberwiseClone();
}