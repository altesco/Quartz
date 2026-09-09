using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public class Trace : BoardEntity
{
    public int Id { get; set; }
    public string NetName { get; set; } = string.Empty; // Имя цепи (GND, VCC)

    public Endpoint From { get; set; } // "(R1.1).1"
    public Endpoint To { get; set; } // "(U1).12"

    public double Width { get; set; } = 0.25; // Ширина дорожки
    public CoordinateMode CoordMode { get; set; } = CoordinateMode.Relative;

    public List<Point2D>? MiddlePoints { get; set; } = [];
}