using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public class Trace : BoardEntity
{
    public Net Net { get; set; } = new();

    public EndpointBase From { get; set; } = new ViaEndpoint();
    public EndpointBase To { get; set; } = new ViaEndpoint();

    public double Width { get; set; } = 0.25;
    public CoordinateMode CoordMode { get; set; } = CoordinateMode.Relative;

    public List<Point2D>? Points { get; set; } = [];

    public override Trace Clone()
    {
        var clone = (Trace)MemberwiseClone();
        clone.Net = Net.Clone();
        clone.From = From.Clone();
        clone.To = To.Clone();
        clone.Points = Points?.ToList();
        return clone;
    }
}