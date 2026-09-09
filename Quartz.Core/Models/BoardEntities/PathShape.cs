namespace Quartz.Core.Models.BoardEntities;

public class PathShape : Shape
{
    public Point2D StartPoint { get; set; }
    public List<Segment>? Segments { get; set; }
}