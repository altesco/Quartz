using System.Numerics;
using Quartz.Core.Enums;
using Quartz.Core.Interfaces;

namespace Quartz.Core.Models.BoardEntities;

public class LinePrimitive : IDrawingPrimitive
{
    public PrimitiveType Type { get; init; }
    public float X1 { get; init; }
    public float Y1 { get; init; }
    public float X2 { get; init; }
    public float Y2 { get; init; }
    public float Thickness { get; init; }
}

public class PolylinePrimitive : IDrawingPrimitive
{
    public PrimitiveType Type { get; init; }
    public List<Vector2> Points { get; set; } = [];
    public float Thickness { get; init; }
}

public class CirclePrimitive : IDrawingPrimitive
{
    public PrimitiveType Type { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Radius { get; init; }
    public bool IsFilled { get; init; }
}

public class RectanglePrimitive : IDrawingPrimitive
{
    public PrimitiveType Type { get; init; }
    public float X { get; init; } // Центр симметрии по X
    public float Y { get; init; } // Центр симметрии по Y
    public float Width { get; init; }
    public float Height { get; init; }
    public float CornerRadius { get; init; } // 0 = острые углы, Width/2 = круг (при Width==Height)
    public bool IsFilled { get; init; }
}

public class PathPrimitive : IDrawingPrimitive
{
    public PrimitiveType Type { get; init; }
    public Vector2 StartPoint { get; init; }
    public List<Segment> Segments { get; init; } = [];
    public bool IsFilled { get; init; }
}

public class TextPrimitive : IDrawingPrimitive
{
    public PrimitiveType Type { get; init; } = PrimitiveType.Text;
    public string Text { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public float FontSize { get; init; }
}