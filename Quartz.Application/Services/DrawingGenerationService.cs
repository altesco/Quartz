using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using System.Numerics;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;

namespace Quartz.Application.Services;

public class DrawingGenerationService : IDrawingGenerationService
{
    public List<DrawingPrimitive> Generate(LayerModel model)
    {
        List<DrawingPrimitive> geometryLayer = [];
        List<DrawingPrimitive> textLayer = [];

        // отрисовка слоя
        DrawingPrimitive layerPrimitive;

        switch (model.Shape)
        {
            case RectShape rect:
                layerPrimitive = new RectanglePrimitive
                {
                    Type = PrimitiveType.ComponentOutline,
                    Width = (float)rect.Width,
                    Height = (float)rect.Height,
                    CornerRadius = (float)rect.CornerRadius
                };
                break;

            case PathShape path:
            {
                layerPrimitive = new PathPrimitive
                {
                    Type = PrimitiveType.BoardOutline,
                    StartPoint = new Vector2(
                        (float)(path.StartPoint.X),
                        (float)(path.StartPoint.Y)
                    ),
                    Segments = path.Segments ?? []
                };

                break;
            }

            default:
                layerPrimitive = new RectanglePrimitive();
                break;
        }

        geometryLayer.Add(layerPrimitive);

        // Отрисовка компонентов и падов
        foreach (var comp in model.Components)
        {
            DrawingPrimitive primitive;

            switch (comp.Shape)
            {
                case RectShape rect:
                    primitive = new RectanglePrimitive
                    {
                        Type = PrimitiveType.ComponentOutline,
                        Width = (float)rect.Width,
                        Height = (float)rect.Height,
                        CornerRadius = (float)rect.CornerRadius,
                        X = (float)comp.Point.X,
                        Y = (float)comp.Point.Y
                    };
                    break;

                case PathShape path:
                {
                    var offsetX = comp.Point.X;
                    var offsetY = comp.Point.Y;

                    primitive = new PathPrimitive
                    {
                        Type = PrimitiveType.ComponentOutline,

                        StartPoint = new Vector2(
                            (float)(path.StartPoint.X + offsetX),
                            (float)(path.StartPoint.Y + offsetY)
                        ),

                        Segments = TranslateSegments(
                            path.Segments,
                            offsetX,
                            offsetY)
                    };

                    break;
                }

                default:
                    primitive = new RectanglePrimitive();
                    break;
            }

            geometryLayer.Add(primitive);

            if (comp.NameSettings.IsVisible)
            {
                textLayer.Add(new TextPrimitive
                {
                    Type = PrimitiveType.Text,
                    Text = comp.Name,
                    X = (float)(comp.Point.X + comp.NameSettings.OffsetX),
                    Y = (float)(comp.Point.Y + comp.NameSettings.OffsetY),
                    FontSize = (float)comp.NameSettings.FontSize
                });
            }

            foreach (var pin in comp.Pins)
            {
                switch (pin.Shape)
                {
                    case RectShape rect:
                        primitive = new RectanglePrimitive
                        {
                            Type = PrimitiveType.Pad,
                            Width = (float)rect.Width,
                            Height = (float)rect.Height,
                            CornerRadius = (float)rect.CornerRadius,
                            X = pin.CoordMode == CoordinateMode.Relative
                                ? (float)(pin.Point.X + comp.Point.X)
                                : (float)pin.Point.X,
                            Y = pin.CoordMode == CoordinateMode.Relative
                                ? (float)(pin.Point.Y + comp.Point.Y)
                                : (float)pin.Point.Y
                        };
                        break;

                    case PathShape path:
                    {
                        var pinPoint = new Vector2
                        {
                            X = pin.CoordMode == CoordinateMode.Relative
                                ? (float)(pin.Point.X + comp.Point.X)
                                : (float)pin.Point.X,

                            Y = pin.CoordMode == CoordinateMode.Relative
                                ? (float)(pin.Point.Y + comp.Point.Y)
                                : (float)pin.Point.Y
                        };

                        var start = new Vector2(
                            (float)path.StartPoint.X,
                            (float)path.StartPoint.Y
                        );

                        primitive = new PathPrimitive
                        {
                            Type = PrimitiveType.Pad,

                            StartPoint = pinPoint + start,

                            Segments = TranslateSegments(
                                path.Segments,
                                pinPoint.X,
                                pinPoint.Y)
                        };

                        break;
                    }

                    default:
                        primitive = new RectanglePrimitive();
                        break;
                }

                geometryLayer.Add(primitive);
            }
        }

        // Отрисовка трасс
        foreach (var trace in model.Traces)
        {
            var primitive = new PolylinePrimitive { Type = PrimitiveType.Trace };

            var start = new Vector2
            {
                X = (float)trace.From.Pin.Point.X + (float)trace.From.Comp.Point.X,
                Y = (float)trace.From.Pin.Point.Y + (float)trace.From.Comp.Point.Y
            };

            var end = new Vector2
            {
                X = (float)trace.To.Pin.Point.X + (float)trace.To.Comp.Point.X,
                Y = (float)trace.To.Pin.Point.Y + (float)trace.To.Comp.Point.Y
            };

            // Старт трассы из центра начального пина
            primitive.Points.Add(start);

            // Промежуточные точки
            if (trace.MiddlePoints != null)
            {
                foreach (var p in trace.MiddlePoints)
                {
                    var pt = trace.CoordMode == CoordinateMode.Relative
                        ? new Vector2((float)p.X + start.X, (float)p.Y + start.Y)
                        : new Vector2((float)p.X, (float)p.Y);

                    primitive.Points.Add(pt);
                }
            }

            // Финиш трассы в центре конечного пина
            primitive.Points.Add(end);

            geometryLayer.Add(primitive);
        }

        return [.. geometryLayer, .. textLayer];
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