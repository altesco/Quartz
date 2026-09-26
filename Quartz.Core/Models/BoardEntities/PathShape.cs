namespace Quartz.Core.Models.BoardEntities;

public class PathShape : Shape
{
    public Point2D StartPoint { get; set; }
    public List<Segment>? Segments { get; set; }

    public override PathShape Clone()
    {
        var clone = (PathShape)MemberwiseClone();
        clone.Segments = Segments?.Select(s => s.Clone()).ToList();
        return clone;
    }
}