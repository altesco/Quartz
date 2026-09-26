namespace Quartz.Core.Models.BoardEntities.Styles;

public class PathShapeStyle : ShapeStyle
{
    public Point2D? StartPoint { get; set; }
    public List<Segment>? Segments { get; set; }

    public override PathShapeStyle Clone()
    {
        var clone = (PathShapeStyle)MemberwiseClone();
        clone.Segments = Segments?.Select(s => s.Clone()).ToList();
        return clone;
    }
}