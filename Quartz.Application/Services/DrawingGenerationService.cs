using System.Numerics;
using System.Runtime.CompilerServices;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Application.Services;

public class DrawingGenerationService : IDrawingGenerationService
{
    private readonly ConditionalWeakTable<BoardModel, Dictionary<string, string>> _compToLayerCache = new();

    public List<DrawingPrimitive> GenerateLayerPrimitives(LayerModel model, BoardModel? board = null)
    {
        List<DrawingPrimitive> primitives = [];

        primitives.Add(GetShapePrimitive(model.Shape, new Point2D(0, 0), PrimitiveType.BoardOutline));

        foreach (var comp in model.Components.Values)
        {
            primitives.Add(GetShapePrimitive(comp.Shape, comp.Point, PrimitiveType.ComponentOutline));

            foreach (var pin in comp.Pins.Values)
            {
                primitives.Add(GetShapePrimitive(
                    pin.Shape,
                    new Point2D(pin.Point.X + comp.Point.X, pin.Point.Y + comp.Point.Y),
                    PrimitiveType.Pin));
            }

            primitives.Add(GetShapePrimitive(comp.Footprint.Shape, comp.Point, PrimitiveType.Footprint));

            foreach (var pad in comp.Footprint.Pads.Values)
            {
                primitives.Add(GetShapePrimitive(
                    pad.Shape,
                    new Point2D(pad.Point.X + comp.Point.X, pad.Point.Y + comp.Point.Y),
                    PrimitiveType.Pad));
            }
        }

        foreach (var trace in model.Traces)
        {
            var polylinePrimitive = new PolylinePrimitive
            {
                Type = PrimitiveType.Trace,
                Thickness = (float)trace.Width
            };

            var start = new Vector2();
            if (trace.From is PadEndpoint padEndpointFrom)
            {
                start.X = (float)(padEndpointFrom.Pad.Point.X + padEndpointFrom.Comp.Point.X);
                start.Y = (float)(padEndpointFrom.Pad.Point.Y + padEndpointFrom.Comp.Point.Y);
            }
            else if (trace.From is ViaEndpoint viaEndpointFrom)
            {
                start.X = (float)viaEndpointFrom.Via.Point.X;
                start.Y = (float)viaEndpointFrom.Via.Point.Y;
            }

            var end = new Vector2();
            if (trace.To is PadEndpoint padEndpointTo)
            {
                end.X = (float)(padEndpointTo.Pad.Point.X + padEndpointTo.Comp.Point.X);
                end.Y = (float)(padEndpointTo.Pad.Point.Y + padEndpointTo.Comp.Point.Y);
            }
            else if (trace.To is ViaEndpoint viaEndpointTo)
            {
                end.X = (float)viaEndpointTo.Via.Point.X;
                end.Y = (float)viaEndpointTo.Via.Point.Y;
            }

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

        if (board != null)
        {
            primitives.AddRange(GenerateLayerNetPrimitives(model, board));
        }

        return primitives;
    }

    private List<DrawingPrimitive> GenerateLayerNetPrimitives(LayerModel currentLayer, BoardModel board)
    {
        var primitives = new List<DrawingPrimitive>();

        var compToLayer = _compToLayerCache.GetValue(board, b => b.Layers
            .SelectMany(l => l.Value.Components.Keys.Select(c => (Comp: c, Layer: l.Key)))
            .ToDictionary(x => x.Comp, x => x.Layer, StringComparer.OrdinalIgnoreCase));

        foreach (var net in board.Nets.Values)
        {
            for (int i = 0; i < net.Nodes.Count - 1; i++)
            {
                var nodeA = net.Nodes[i];
                var nodeB = net.Nodes[i + 1];

                if (!compToLayer.TryGetValue(nodeA.Comp.Name, out var layerA) ||
                    !compToLayer.TryGetValue(nodeB.Comp.Name, out var layerB))
                    continue;

                var posA = GetGlobalPadPoint(nodeA);
                var posB = GetGlobalPadPoint(nodeB);

                bool isCurrentA = string.Equals(layerA, currentLayer.Name, StringComparison.OrdinalIgnoreCase);
                bool isCurrentB = string.Equals(layerB, currentLayer.Name, StringComparison.OrdinalIgnoreCase);

                if (isCurrentA && isCurrentB)
                {
                    primitives.Add(CreateNetSegment(posA, posB));
                }
                else if (isCurrentA)
                {
                    var key = new NodeViaKey(nodeA.Comp.Name, nodeA.Pad.Name, layerA, layerB);
                    if (board.NearestVias.TryGetValue(key, out var nearestVia))
                    {
                        var viaPos = new Vector2((float)nearestVia.Point.X, (float)nearestVia.Point.Y);
                        primitives.Add(CreateNetSegment(posA, viaPos));
                    }
                }
                else if (isCurrentB)
                {
                    var key = new NodeViaKey(nodeB.Comp.Name, nodeB.Pad.Name, layerB, layerA);
                    if (board.NearestVias.TryGetValue(key, out var nearestVia))
                    {
                        var viaPos = new Vector2((float)nearestVia.Point.X, (float)nearestVia.Point.Y);
                        primitives.Add(CreateNetSegment(viaPos, posB));
                    }
                }
            }
        }

        return primitives;
    }

    private static Vector2 GetGlobalPadPoint(PadEndpoint node)
    {
        return new Vector2(
            (float)(node.Pad.Point.X + node.Comp.Point.X),
            (float)(node.Pad.Point.Y + node.Comp.Point.Y)
        );
    }

    private static PolylinePrimitive CreateNetSegment(Vector2 start, Vector2 end)
    {
        var poly = new PolylinePrimitive { Type = PrimitiveType.Net };
        poly.Points.Add(start);
        poly.Points.Add(end);
        return poly;
    }

    public List<DrawingPrimitive> GenerateBoardOverlayPrimitives(BoardModel board)
    {
        List<DrawingPrimitive> primitives = [];

        foreach (var via in board.Vias.Values)
        {
            primitives.Add(GetShapePrimitive(via.Shape, via.Point, PrimitiveType.Via));
        }

        return primitives;
    }

    private static DrawingPrimitive GetShapePrimitive(Shape? shape, Point2D startPoint, PrimitiveType type)
    {
        if (shape == null)
        {
            return new RectanglePrimitive
            {
                Type = type,
                X = (float)startPoint.X,
                Y = (float)startPoint.Y,
                Width = 0,
                Height = 0
            };
        }

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
                return new RectanglePrimitive
                {
                    Type = type,
                    X = (float)startPoint.X,
                    Y = (float)startPoint.Y
                };
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