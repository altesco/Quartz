using System.Numerics;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Application.Services;

public class DrawingGenerationService : IDrawingGenerationService
{
    public List<DrawingPrimitive> GenerateLayerPrimitives(LayerModel model)
    {
        List<DrawingPrimitive> primitives = [];

        if (model.Shape != null)
        {
            primitives.Add(GetShapePrimitive(model.Shape, new Point2D(0, 0), PrimitiveType.BoardOutline));
        }

        foreach (var comp in model.Components.Values)
        {
            if (comp.Shape != null)
            {
                primitives.Add(GetShapePrimitive(comp.Shape, comp.Point, PrimitiveType.ComponentOutline));
            }

            foreach (var pin in comp.Pins.Values)
            {
                if (pin.Shape != null)
                {
                    primitives.Add(GetShapePrimitive(
                        pin.Shape,
                        new Point2D(pin.Point.X + comp.Point.X, pin.Point.Y + comp.Point.Y),
                        PrimitiveType.Pin));
                }
            }

            if (comp.Footprint?.Shape != null)
            {
                primitives.Add(GetShapePrimitive(comp.Footprint.Shape, comp.Point, PrimitiveType.Footprint));

                foreach (var pad in comp.Footprint.Pads.Values)
                {
                    if (pad.Shape != null)
                    {
                        primitives.Add(GetShapePrimitive(
                            pad.Shape,
                            new Point2D(pad.Point.X + comp.Point.X, pad.Point.Y + comp.Point.Y),
                            PrimitiveType.Pad));
                    }
                }
            }
        }

        foreach (var trace in model.Traces)
        {
            var polylinePrimitive = new PolylinePrimitive
            {
                Type = PrimitiveType.Trace,
                Thickness = (float)trace.Width
            };

            var start = new Vector2
            {
                X = (float)(trace.From.Pad.Point.X + trace.From.Comp.Point.X),
                Y = (float)(trace.From.Pad.Point.Y + trace.From.Comp.Point.Y)
            };

            var end = new Vector2
            {
                X = (float)(trace.To.Pad.Point.X + trace.To.Comp.Point.X),
                Y = (float)(trace.To.Pad.Point.Y + trace.To.Comp.Point.Y)
            };

            polylinePrimitive.Points.Add(start);

            if (trace.Points != null)
            {
                foreach (var p in trace.Points)
                {
                    var pt = trace.CoordMode == CoordinateMode.Relative
                        ? new Vector2((float)p.X + start.X, (float)p.Y + start.Y)
                        : new Vector2((float)p.X, (float)p.Y);

                    polylinePrimitive.Points.Add(pt);
                }
            }

            polylinePrimitive.Points.Add(end);
            primitives.Add(polylinePrimitive);
        }

        foreach (var comp in model.Components.Values)
        {
            if (comp.NameSettings is { IsVisible: true })
            {
                primitives.Add(new TextPrimitive
                {
                    Type = PrimitiveType.Text,
                    Text = comp.Name,
                    X = (float)(comp.Point.X + comp.NameSettings.OffsetX),
                    Y = (float)(comp.Point.Y + comp.NameSettings.OffsetY),
                    FontSize = (float)comp.NameSettings.FontSize
                });
            }
        }

        return primitives;
    }

    /// <summary>
    /// Безопасно генерирует примитивы для Vias и Nets с проверками на null.
    /// </summary>
    public List<DrawingPrimitive> GenerateBoardOverlayPrimitives(BoardModel board)
    {
        List<DrawingPrimitive> primitives = [];

        // 1. Межслойные переходные отверстия (Vias)
        foreach (var via in board.Vias.Values)
        {
            if (via.Shape != null)
            {
                primitives.Add(GetShapePrimitive(via.Shape, via.Point, PrimitiveType.Via));
            }
        }

        // 2. Связи / Airwires (Nets)
        foreach (var net in board.Nets.Values)
        {
            var netPrimitive = new PolylinePrimitive { Type = PrimitiveType.Net };

            foreach (var node in net.Nodes)
            {
                // Защита от NullReference, если узлы сети не до конца связались с компонентами/пэдами
                if (node.Comp == null || node.Pad == null) continue;

                netPrimitive.Points.Add(new Vector2
                {
                    X = (float)(node.Pad.Point.X + node.Comp.Point.X),
                    Y = (float)(node.Pad.Point.Y + node.Comp.Point.Y)
                });
            }

            // Добавляем примитив только если есть минимум 2 валидные точки для отрисовки линии
            if (netPrimitive.Points.Count >= 2)
            {
                primitives.Add(netPrimitive);
            }
        }

        return primitives;
    }

    private static DrawingPrimitive GetShapePrimitive(Shape shape, Point2D startPoint, PrimitiveType type)
    {
        switch (shape)
        {
            case RectShape rect:
                return new RectanglePrimitive
                {
                    Type = type,
                    Width = (float)rect.Width,
                    Height = (float)rect.Height,
                    CornerRadius = (float)rect.CornerRadius,
                    X = (float)startPoint.X,
                    Y = (float)startPoint.Y
                };

            case PathShape path:
                return new PathPrimitive
                {
                    Type = type,
                    StartPoint = new Vector2(
                        (float)(path.StartPoint.X + startPoint.X),
                        (float)(path.StartPoint.Y + startPoint.Y)
                    ),
                    Segments = TranslateSegments(
                        path.Segments ?? [],
                        startPoint.X,
                        startPoint.Y)
                };

            default:
                return new RectanglePrimitive();
        }
    }

    private static List<Segment> TranslateSegments(
        IEnumerable<Segment> segments,
        double offsetX,
        double offsetY)
    {
        var result = new List<Segment>();

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case LineSegment line:
                    result.Add(new LineSegment
                    {
                        Point = new Point2D
                        {
                            X = line.Point.X + offsetX,
                            Y = line.Point.Y + offsetY
                        }
                    });
                    break;

                case ArcSegment arc:
                    result.Add(new ArcSegment
                    {
                        Point = new Point2D
                        {
                            X = arc.Point.X + offsetX,
                            Y = arc.Point.Y + offsetY
                        },
                        Radius = arc.Radius,
                        IsClockwise = arc.IsClockwise,
                        IsLargeArc = arc.IsLargeArc
                    });
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unknown segment type: {segment.GetType().Name}");
            }
        }

        return result;
    }
}