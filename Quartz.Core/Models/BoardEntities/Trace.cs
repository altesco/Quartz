using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public class Trace : BoardEntity
{
    public Net Net { get; set; } = new(); // Имя цепи (GND, VCC)

    public EndpointBase From { get; set; } = new ViaEndpoint();
    public EndpointBase To { get; set; } = new ViaEndpoint();

    public double Width { get; set; } = 0.25; // Ширина дорожки
    public CoordinateMode CoordMode { get; set; } = CoordinateMode.Relative;

    public List<Point2D>? Points { get; set; } = [];
}