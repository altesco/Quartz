

namespace Quartz.Core.Models.BoardEntities.Styles;

public class PathShapeStyle : ShapeStyle
{
    public Point2D? StartPoint { get; set; }
    public List<Segment>? Segments { get; set; }
}