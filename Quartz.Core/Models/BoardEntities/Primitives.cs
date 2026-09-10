using System.Numerics;
using Quartz.Core.Interfaces;

namespace Quartz.Core.Models.BoardEntities;

public class LinePrimitive : DrawingPrimitive
{
    public float X1 { get; init; }
    public float Y1 { get; init; }
    public float X2 { get; init; }
    public float Y2 { get; init; }
    public float Thickness { get; init; }
}

public class PolylinePrimitive : DrawingPrimitive
{
    public List<Vector2> Points { get; set; } = [];
    public float Thickness { get; init; }
}

public class CirclePrimitive : DrawingPrimitive
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Radius { get; init; }
}

public class RectanglePrimitive : DrawingPrimitive
{
    public float X { get; init; } // Центр симметрии по X
    public float Y { get; init; } // Центр симметрии по Y
    public float Width { get; init; }
    public float Height { get; init; }
    public float CornerRadius { get; init; } // 0 = острые углы, Width/2 = круг (при Width==Height)
}

public class PathPrimitive : DrawingPrimitive
{
    public Vector2 StartPoint { get; init; }
    public List<Segment> Segments { get; init; } = [];
}

public class TextPrimitive : DrawingPrimitive
{
    public string Text { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public float FontSize { get; init; }
}