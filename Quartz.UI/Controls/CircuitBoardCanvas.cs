using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Quartz.Core.Enums;
using Quartz.Core.Interfaces;
using Quartz.Core.Models.BoardEntities;
using SkiaSharp;

namespace Quartz.UI.Controls;

public class CircuitBoardCanvas : SKCanvasView, IDisposable
{
    // Данные для отрисовки
    public static readonly StyledProperty<IReadOnlyList<IDrawingPrimitive>?> DrawingDataProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, IReadOnlyList<IDrawingPrimitive>?>(nameof(DrawingData));

    public IReadOnlyList<IDrawingPrimitive>? DrawingData
    {
        get => GetValue(DrawingDataProperty);
        set => SetValue(DrawingDataProperty, value);
    }

    // РЕШЕНИЕ: Переносим состояние трансформации в StyledProperty, чтобы их можно было забиндить к VM
    public static readonly StyledProperty<float> ZoomProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(nameof(Zoom), 1.0f);

    public static readonly StyledProperty<float> OffsetXProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(nameof(OffsetX), 0f);

    public static readonly StyledProperty<float> OffsetYProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(nameof(OffsetY), 0f);

    public float Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public float OffsetX
    {
        get => GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    public float OffsetY
    {
        get => GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    // Константы ограничений
    private const float MinZoom = 0.05f;
    private const float MaxZoom = 50.0f;
    private const float WheelPanSensitivity = 15f;

    // Состояние для перетаскивания мыши
    private bool _isDragging;
    private Point _lastPointerPosition;

    // Состояние для ручного щипка
    private readonly Dictionary<int, Point> _trackedPointers = [];
    private float _lastPinchDistance;
    private Point _lastPinchMidpoint;
    private bool _isPinchingManual;

    private readonly Dictionary<PrimitiveType, SKPaint> _paintCache = [];

    public CircuitBoardCanvas()
    {
        InitializePaintCache();
        Focusable = true;
    }

    private void InitializePaintCache()
    {
        _paintCache[PrimitiveType.Trace] = new SKPaint
        {
            Color = SKColors.DarkCyan, 
            Style = SKPaintStyle.Stroke, 
            IsAntialias = true, 
            StrokeWidth = 1.5f
        };
        _paintCache[PrimitiveType.Pad] = new SKPaint
        {
            Color = SKColors.Goldenrod, 
            Style = SKPaintStyle.Fill, 
            IsAntialias = true
        };
        _paintCache[PrimitiveType.ComponentOutline] = new SKPaint
        {
            Color = SKColors.LightGray, 
            Style = SKPaintStyle.Stroke, 
            StrokeWidth = 1.5f, 
            IsAntialias = true
        };
        _paintCache[PrimitiveType.Text] = new SKPaint
        {
            Color = SKColors.White, 
            IsAntialias = true
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Если снаружи (из VM) прилетели новые координаты или данные — перерисовываем
        if (change.Property == DrawingDataProperty ||
            change.Property == ZoomProperty ||
            change.Property == OffsetXProperty ||
            change.Property == OffsetYProperty)
        {
            InvalidateSurface();
        }
    }

    #region Input Handlers

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _trackedPointers[e.Pointer.Id] = e.GetPosition(this);

        if (_trackedPointers.Count == 2)
        {
            _isDragging = false;
            var points = new List<Point>(_trackedPointers.Values);
            _lastPinchDistance = GetDistance(points[0], points[1]);
            _lastPinchMidpoint = GetMidpoint(points[0], points[1]);
            _isPinchingManual = true;
            e.Handled = true;
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed || properties.IsMiddleButtonPressed)
        {
            _isDragging = true;
            _lastPointerPosition = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_trackedPointers.ContainsKey(e.Pointer.Id))
        {
            _trackedPointers[e.Pointer.Id] = e.GetPosition(this);
        }

        // Щипок двумя пальцами
        if (_isPinchingManual && _trackedPointers.Count == 2)
        {
            var points = new List<Point>(_trackedPointers.Values);
            float currentDistance = GetDistance(points[0], points[1]);
            Point currentMidpoint = GetMidpoint(points[0], points[1]);

            if (_lastPinchDistance > 0.1f)
            {
                float oldZoom = Zoom;
                float scaleFactor = currentDistance / _lastPinchDistance;
                Zoom = Math.Clamp(Zoom * scaleFactor, MinZoom, MaxZoom);

                float panX = (float)(currentMidpoint.X - _lastPinchMidpoint.X);
                float panY = (float)(currentMidpoint.Y - _lastPinchMidpoint.Y);

                OffsetX = (float)(currentMidpoint.X - (currentMidpoint.X - OffsetX) * (Zoom / oldZoom)) + panX;
                OffsetY = (float)(currentMidpoint.Y - (currentMidpoint.Y - OffsetY) * (Zoom / oldZoom)) + panY;
            }

            _lastPinchDistance = currentDistance;
            _lastPinchMidpoint = currentMidpoint;
            e.Handled = true;
            return;
        }

        // Обычный драг мыши
        if (_isDragging && !_isPinchingManual)
        {
            var currentPos = e.GetPosition(this);
            OffsetX += (float)(currentPos.X - _lastPointerPosition.X);
            OffsetY += (float)(currentPos.Y - _lastPointerPosition.Y);
            _lastPointerPosition = currentPos;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _trackedPointers.Remove(e.Pointer.Id);

        if (_trackedPointers.Count < 2) _isPinchingManual = false;

        if (_isDragging && _trackedPointers.Count == 0)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var mousePos = e.GetPosition(this);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            float oldZoom = Zoom;
            float zoomFactor = e.Delta.Y > 0 ? 1.1f : 1.0f / 1.1f;
            Zoom = Math.Clamp(Zoom * zoomFactor, MinZoom, MaxZoom);

            OffsetX = (float)(mousePos.X - (mousePos.X - OffsetX) * (Zoom / oldZoom));
            OffsetY = (float)(mousePos.Y - (mousePos.Y - OffsetY) * (Zoom / oldZoom));
            e.Handled = true;
        }
        else
        {
            OffsetX += (float)e.Delta.X * WheelPanSensitivity;
            OffsetY += (float)e.Delta.Y * WheelPanSensitivity;
            e.Handled = true;
        }
    }

    private static float GetDistance(Point p1, Point p2) =>
        (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

    private static Point GetMidpoint(Point p1, Point p2) =>
        new((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);

    #endregion

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);

        var primitives = DrawingData;
        if (primitives == null || primitives.Count == 0) return;

        canvas.Save();
        canvas.Translate(OffsetX, OffsetY);
        canvas.Scale(Zoom);

        foreach (var primitive in primitives)
        {
            if (!_paintCache.TryGetValue(primitive.Type, out var paint)) continue;

            switch (primitive)
            {
                case LinePrimitive line:
                    paint.StrokeWidth = line.Thickness;
                    canvas.DrawLine(line.X1, line.Y1, line.X2, line.Y2, paint);
                    break;
                
                case PolylinePrimitive polyline:
                {
                    using var path = new SKPath();
                    var points = polyline.Points
                        .Select(item => new SKPoint
                        {
                            X = item.X,
                            Y = item.Y
                        })
                        .ToArray();
                    path.MoveTo(points[0]);
                    for (int i = 1; i < points.Length; i++)
                    {
                        path.LineTo(points[i]);
                    }
                    paint.StrokeJoin = SKStrokeJoin.Round;
                    canvas.DrawPath(path, paint);
                    break;
                }

                case CirclePrimitive circle:
                    paint.Style = circle.IsFilled ? SKPaintStyle.Fill : SKPaintStyle.Stroke;
                    canvas.DrawCircle(circle.X, circle.Y, circle.Radius, paint);
                    break;

                case RectanglePrimitive rect:
                    paint.Style = rect.IsFilled ? SKPaintStyle.Fill : SKPaintStyle.Stroke;
                    var skRect = new SKRect(rect.X - rect.Width / 2f, rect.Y - rect.Height / 2f,
                        rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

                    if (rect.CornerRadius > 0f)
                        canvas.DrawRoundRect(skRect, rect.CornerRadius, rect.CornerRadius, paint);
                    else
                        canvas.DrawRect(skRect, paint);
                    break;
                
                case PathPrimitive pathPrimitive:
                {
                    if (pathPrimitive.Segments.Count == 0) break;

                    using var skPath = new SKPath();
                    skPath.MoveTo(pathPrimitive.StartPoint.X, pathPrimitive.StartPoint.Y);

                    foreach (var segment in pathPrimitive.Segments)
                    {
                        switch (segment)
                        {
                            case ArcSegment arc:
                                skPath.ArcTo(
                                    rx: (float)arc.Radius,
                                    ry: (float)arc.Radius,
                                    xAxisRotate: 0f,
                                    largeArc: arc.IsLargeArc ? SKPathArcSize.Large : SKPathArcSize.Small,
                                    sweep: arc.IsClockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise,
                                    x: (float)arc.Point.X,
                                    y: (float)arc.Point.Y
                                );
                                break;

                            default:
                                skPath.LineTo((float)segment.Point.X, (float)segment.Point.Y);
                                break;
                        }
                    }

                    var originalStyle = paint.Style;
                    var originalWidth = paint.StrokeWidth;

                    paint.Style = pathPrimitive.IsFilled ? SKPaintStyle.Fill : SKPaintStyle.Stroke;
                    if (!pathPrimitive.IsFilled)
                    {
                        paint.StrokeJoin = SKStrokeJoin.Round;
                        paint.StrokeCap = SKStrokeCap.Round;
                    }

                    canvas.DrawPath(skPath, paint);

                    paint.Style = originalStyle;
                    paint.StrokeWidth = originalWidth;
                    break;
                }

                case TextPrimitive text:
                    using (var font = new SKFont(SKTypeface.Default, text.FontSize))
                    {
                        canvas.DrawText(text.Text, text.X, text.Y, font, paint);
                    }

                    break;
            }
        }

        canvas.Restore();
    }

    public void Dispose()
    {
        foreach (var paint in _paintCache.Values) 
            paint.Dispose();
        _paintCache.Clear();
    }
}