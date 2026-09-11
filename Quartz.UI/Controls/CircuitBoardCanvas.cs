using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;
using SkiaSharp;

namespace Quartz.UI.Controls;

public class CircuitBoardCanvas : SKCanvasView, IDisposable
{
    public static readonly StyledProperty<IReadOnlyList<DrawingPrimitive>?> DrawingDataProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, IReadOnlyList<DrawingPrimitive>?>(nameof(DrawingData));

    public IReadOnlyList<DrawingPrimitive>? DrawingData
    {
        get => GetValue(DrawingDataProperty);
        set => SetValue(DrawingDataProperty, value);
    }

    public static readonly StyledProperty<float> ZoomProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(nameof(Zoom), 1.0f);

    public static readonly StyledProperty<float> OffsetXProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(nameof(OffsetX));

    public static readonly StyledProperty<float> OffsetYProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(nameof(OffsetY));

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

    private const float MinZoom = 0.05f;
    private const float MaxZoom = 50.0f;
    private const float WheelPanSensitivity = 15f;
    private const float BasePixelsPerMm = 96f / 25.4f;

    private bool _isDragging;
    private Point _lastPointerPosition;

    private readonly Dictionary<int, Point> _trackedPointers = [];
    private float _lastPinchDistance;
    private Point _lastPinchMidpoint;
    private bool _isPinchingManual;

    // Переменные для анимации плавной подгонки
    private float _targetZoom = 1.0f;
    private float _targetOffsetX;
    private float _targetOffsetY;
    private readonly DispatcherTimer _animationTimer;

    private readonly Dictionary<PrimitiveType, SKPaint> _paintCache = [];

    public CircuitBoardCanvas()
    {
        InitializePaintCache();
        Focusable = true;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _animationTimer.Tick += OnAnimationTick;
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
        _paintCache[PrimitiveType.BoardOutline] = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomProperty && !_animationTimer.IsEnabled)
            _targetZoom = Zoom;
        if (change.Property == OffsetXProperty && !_animationTimer.IsEnabled)
            _targetOffsetX = OffsetX;
        if (change.Property == OffsetYProperty && !_animationTimer.IsEnabled)
            _targetOffsetY = OffsetY;

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

        // При ручном нажатии останавливаем анимацию зума
        StopAnimation();

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

                SyncTargetsWithCurrent();
            }

            _lastPinchDistance = currentDistance;
            _lastPinchMidpoint = currentMidpoint;
            e.Handled = true;
            return;
        }

        if (_isDragging && !_isPinchingManual)
        {
            var currentPos = e.GetPosition(this);
            OffsetX += (float)(currentPos.X - _lastPointerPosition.X);
            OffsetY += (float)(currentPos.Y - _lastPointerPosition.Y);
            _lastPointerPosition = currentPos;
            SyncTargetsWithCurrent();
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
            // Возвращаем честные 10% за каждый щелчок колесика!
            float zoomFactor = e.Delta.Y > 0 ? 1.2f : 1.0f / 1.2f;

            // Считаем целевые значения от текущих таргетов, чтобы быстрый скролл накопился
            float newTargetZoom = Math.Clamp(_targetZoom * zoomFactor, MinZoom, MaxZoom);

            _targetOffsetX = (float)(mousePos.X - (mousePos.X - _targetOffsetX) * (newTargetZoom / _targetZoom));
            _targetOffsetY = (float)(mousePos.Y - (mousePos.Y - _targetOffsetY) * (newTargetZoom / _targetZoom));
            _targetZoom = newTargetZoom;

            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }
        }
        else
        {
            StopAnimation();
            OffsetX += (float)e.Delta.X * WheelPanSensitivity;
            OffsetY += (float)e.Delta.Y * WheelPanSensitivity;
            SyncTargetsWithCurrent();
        }

        e.Handled = true;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        const float smoothing = 0.25f; // Скорость доводки (чем выше, тем быстрее догоняет)

        float zoomDiff = Math.Abs(_targetZoom - Zoom);
        float offsetXDiff = Math.Abs(_targetOffsetX - OffsetX);
        float offsetYDiff = Math.Abs(_targetOffsetY - OffsetY);

        if (zoomDiff < 0.0001f && offsetXDiff < 0.01f && offsetYDiff < 0.01f)
        {
            Zoom = _targetZoom;
            OffsetX = _targetOffsetX;
            OffsetY = _targetOffsetY;
            _animationTimer.Stop();
            return;
        }

        Zoom += (_targetZoom - Zoom) * smoothing;
        OffsetX += (_targetOffsetX - OffsetX) * smoothing;
        OffsetY += (_targetOffsetY - OffsetY) * smoothing;
    }

    private void SyncTargetsWithCurrent()
    {
        _targetZoom = Zoom;
        _targetOffsetX = OffsetX;
        _targetOffsetY = OffsetY;
    }

    private void StopAnimation()
    {
        if (_animationTimer.IsEnabled)
        {
            _animationTimer.Stop();
        }

        SyncTargetsWithCurrent();
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

        DrawAdaptiveGrid(canvas);

        var primitives = DrawingData;
        if (primitives == null || primitives.Count == 0)
            return;

        canvas.Save();
        canvas.Translate(OffsetX, OffsetY);
        float totalScale = BasePixelsPerMm * Zoom;
        canvas.Scale(totalScale);

        foreach (var primitive in primitives)
        {
            if (!_paintCache.TryGetValue(primitive.Type, out var paint)) continue;

            // Если это контур компонента или границы платы — держим толщину постоянной на экране
            if (primitive.Type is PrimitiveType.ComponentOutline or PrimitiveType.BoardOutline)
            {
                float basePixelWidth = primitive switch
                {
                    LinePrimitive line when line.Thickness > 0 => line.Thickness,
                    _ => primitive.Type == PrimitiveType.BoardOutline ? 2.0f : 1.5f
                };

                // Компенсируем масштаб канваса: экранные пиксели / totalScale
                paint.StrokeWidth = basePixelWidth / totalScale;
            }
            else if (primitive is LinePrimitive line)
            {
                // Для остальных линий (например, реальных дорожек Trace) сохраняем их физический размер
                paint.StrokeWidth = line.Thickness;
            }
            

            switch (primitive)
            {
                case LinePrimitive line:
                    canvas.DrawLine(line.X1, line.Y1, line.X2, line.Y2, paint);
                    break;

                case PolylinePrimitive polyline:
                {
                    using var path = new SKPath();
                    var points = polyline.Points
                        .Select(item => new SKPoint { X = item.X, Y = item.Y })
                        .ToArray();

                    if (points.Length > 0)
                    {
                        path.MoveTo(points[0]);
                        for (int i = 1; i < points.Length; i++)
                        {
                            path.LineTo(points[i]);
                        }

                        // 1. Устанавливаем радиус скругления оси трассы (в мм)
                        // Радиус должен быть БОЛЬШЕ половины ширины трассы (например, 1.0 мм при ширине 0.5 мм)
                        float traceWidth = polyline.Thickness > 0 ? polyline.Thickness : 0.25f;
                        float cornerRadius = traceWidth * 1.5f; // или задавай через свойство polyline.CornerRadius

                        // 2. Применяем эффект скругления углов геометрии
                        using var cornerEffect = SKPathEffect.CreateCorner(cornerRadius);

                        paint.StrokeWidth = traceWidth;
                        paint.StrokeJoin = SKStrokeJoin.Round;
                        paint.PathEffect = cornerEffect;

                        canvas.DrawPath(path, paint);

                        // Сбрасываем эффект, чтобы он не повлиял на следующие элементы
                        paint.PathEffect = null;
                    }

                    break;
                }

                case CirclePrimitive circle:
                    canvas.DrawCircle(circle.X, circle.Y, circle.Radius, paint);
                    break;

                case RectanglePrimitive rect:
                    var skRect = new SKRect(rect.X - rect.Width / 2f, rect.Y - rect.Height / 2f,
                        rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

                    if (rect.CornerRadius > 0f)
                        canvas.DrawRoundRect(skRect, rect.CornerRadius, rect.CornerRadius, paint);
                    else
                        canvas.DrawRect(skRect, paint);
                    break;

                case PathPrimitive pathPrimitive:
                {
                    if (pathPrimitive.Segments.Count == 0)
                        break;

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
                                    sweep: arc.IsClockwise
                                        ? SKPathDirection.Clockwise
                                        : SKPathDirection.CounterClockwise,
                                    x: (float)arc.Point.X,
                                    y: (float)arc.Point.Y
                                );
                                break;

                            default:
                                skPath.LineTo((float)segment.Point.X, (float)segment.Point.Y);
                                break;
                        }
                    }

                    skPath.Close();
                    canvas.DrawPath(skPath, paint);
                    break;
                }

                case TextPrimitive text:
                {
                    using var font = new SKFont(SKTypeface.Default, text.FontSize);
                    font.MeasureText(text.Text, out var textBounds);
                    var x = text.X - textBounds.MidX;
                    var y = text.Y - textBounds.MidY;
                    canvas.DrawText(text.Text, x, y, font, paint);
                    break;
                }
            }
        }

        canvas.Restore();
    }

    private static float CalculateGridStep(float scale, float targetVisualPixels = 40f)
    {
        float rawD = targetVisualPixels / scale;
        float exponent = MathF.Floor(MathF.Log10(rawD));
        float magnitude = MathF.Pow(10f, exponent);
        float fraction = rawD / magnitude;

        float niceFraction = fraction switch
        {
            < 1.5f => 1f,
            < 3.5f => 2f,
            < 7.5f => 5f,
            _ => 10f
        };

        return niceFraction * magnitude;
    }

    private void DrawAdaptiveGrid(SKCanvas canvas)
    {
        float scale = BasePixelsPerMm * Zoom;
        float d = CalculateGridStep(scale, targetVisualPixels: 40f);

        float width = (float)Bounds.Width;
        float height = (float)Bounds.Height;
        if (width <= 0 || height <= 0) return;

        float minXMm = -OffsetX / scale;
        float minYMm = -OffsetY / scale;
        float maxXMm = (width - OffsetX) / scale;
        float maxYMm = (height - OffsetY) / scale;

        long startX = (long)Math.Floor(minXMm / d);
        long endX = (long)Math.Ceiling(maxXMm / d);
        long startY = (long)Math.Floor(minYMm / d);
        long endY = (long)Math.Ceiling(maxYMm / d);

        using var dotPaint = new SKPaint();
        dotPaint.Color = new SKColor(120, 120, 120, 180);
        dotPaint.IsAntialias = true;
        dotPaint.Style = SKPaintStyle.Fill;

        float dotRadius = 1.2f;

        for (long x = startX; x <= endX; x++)
        {
            float xPx = (x * d) * scale + OffsetX;
            for (long y = startY; y <= endY; y++)
            {
                float yPx = (y * d) * scale + OffsetY;
                canvas.DrawCircle(xPx, yPx, dotRadius, dotPaint);
            }
        }
    }

    public void Dispose()
    {
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;

        foreach (var paint in _paintCache.Values)
            paint.Dispose();
        _paintCache.Clear();
    }
}