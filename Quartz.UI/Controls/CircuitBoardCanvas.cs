using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;
using SkiaSharp;

namespace Quartz.UI.Controls;

public class CircuitBoardCanvas : Control, IDisposable
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

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, IBrush?>(nameof(Background));

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

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
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
    private SKColor _backgroundColor = SKColors.Black;
    private SKColor _gridColor = new SKColor(120, 120, 120, 180);
    private WriteableBitmap? _bitmap;

    public CircuitBoardCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        InitializePaintCache();

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _animationTimer.Tick += OnAnimationTick;
    }

    public void InvalidateSurface()
    {
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;

        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.ActualThemeVariantChanged -= OnGlobalThemeChanged;
            Avalonia.Application.Current.ActualThemeVariantChanged += OnGlobalThemeChanged;
        }

        UpdateThemePaints();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;

        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.ActualThemeVariantChanged -= OnGlobalThemeChanged;
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => UpdateThemePaints();
    private void OnGlobalThemeChanged(object? sender, EventArgs e) => UpdateThemePaints();

    private void InitializePaintCache()
    {
        // 1. Границы платы
        _paintCache[PrimitiveType.BoardOutline] = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f,
            IsAntialias = true
        };

        // 2. Контур компонента
        _paintCache[PrimitiveType.ComponentOutline] = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            IsAntialias = true
        };

        // 3. Посадочное место (Footprint / Шелкография)
        _paintCache[PrimitiveType.Footprint] = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        // 4. Контактные площадки (Pads) — сплошная заливка
        _paintCache[PrimitiveType.Pad] = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // 5. Выводы (Pins) — тонкий контур
        _paintCache[PrimitiveType.Pin] = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        // 6. Трассы (Traces)
        _paintCache[PrimitiveType.Trace] = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeWidth = 1.5f
        };

        // 7. Переходные отверстия (Vias)
        _paintCache[PrimitiveType.Via] = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // 8. Электрические связи (Nets / Airwires) — пунктирная линия
        _paintCache[PrimitiveType.Net] = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([3.0f, 3.0f], 0) // Пунктир для связей
        };

        // 9. Текст
        _paintCache[PrimitiveType.Text] = new SKPaint
        {
            IsAntialias = true
        };

        UpdateThemePaints();
    }

    private void UpdateThemePaints()
    {
        var targetTheme = GetCurrentThemeVariant();

        if (Background is ISolidColorBrush scb)
        {
            _backgroundColor = new SKColor(scb.Color.R, scb.Color.G, scb.Color.B, scb.Color.A);
        }
        else
        {
            _backgroundColor = GetColor("CanvasBackgroundColor", "BackgroundColor", SKColors.Black, targetTheme);
        }

        _gridColor = GetColor("CanvasGridColor", "BorderColor", new SKColor(120, 120, 120, 180), targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.BoardOutline, out var boardPaint))
            boardPaint.Color = GetColor("CanvasBoardOutlineColor", "WarningColor", SKColors.Yellow, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.ComponentOutline, out var compPaint))
            compPaint.Color = GetColor("CanvasComponentOutlineColor", "ForegroundLeadColor", SKColors.LightGray, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.Footprint, out var fpPaint))
            fpPaint.Color = GetColor("CanvasFootprintColor", "MutedColor", SKColors.Silver, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.Pad, out var padPaint))
            padPaint.Color = GetColor("CanvasPadColor", "WarningColor", SKColors.Goldenrod, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.Pin, out var pinPaint))
            pinPaint.Color = GetColor("CanvasPinColor", "SuccessColor", SKColors.SpringGreen, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.Trace, out var tracePaint))
            tracePaint.Color = GetColor("CanvasTraceColor", "PrimaryColor", SKColors.DarkCyan, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.Via, out var viaPaint))
            viaPaint.Color = GetColor("CanvasViaColor", "DestructiveColor", SKColors.DarkOrange, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.Net, out var netPaint))
            netPaint.Color = GetColor("CanvasNetColor", "DestructiveColor", SKColors.DeepPink, targetTheme);

        if (_paintCache.TryGetValue(PrimitiveType.Text, out var textPaint))
            textPaint.Color = GetColor("CanvasTextColor", "ForegroundColor", SKColors.White, targetTheme);

        InvalidateVisual();
    }

    private ThemeVariant GetCurrentThemeVariant()
    {
        if (ActualThemeVariant != ThemeVariant.Default)
            return ActualThemeVariant;

        return Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;
    }

    private SKColor GetColor(string specificKey, string fallbackKey, SKColor hardcodedFallback, ThemeVariant targetTheme)
    {
        if (TryGetThemeColor(specificKey, targetTheme, out var col))
            return col;

        if (!string.IsNullOrEmpty(fallbackKey) && TryGetThemeColor(fallbackKey, targetTheme, out var fallbackCol))
            return fallbackCol;

        return hardcodedFallback;
    }

    private bool TryGetThemeColor(string key, ThemeVariant targetTheme, out SKColor color)
    {
        color = default;
        Visual? current = this;

        while (current != null)
        {
            if (current is IResourceNode node && node.TryGetResource(key, targetTheme, out var res))
            {
                if (TryConvertToSKColor(res, out color))
                    return true;
            }
            current = current.GetVisualParent();
        }

        if (Avalonia.Application.Current is IResourceNode appNode &&
            appNode.TryGetResource(key, targetTheme, out var appRes))
        {
            if (TryConvertToSKColor(appRes, out color))
                return true;
        }

        if (this.TryFindResource(key, out var localRes) && TryConvertToSKColor(localRes, out color))
            return true;

        return false;
    }

    private static bool TryConvertToSKColor(object? resource, out SKColor color)
    {
        if (resource is Avalonia.Media.Color c)
        {
            color = new SKColor(c.R, c.G, c.B, c.A);
            return true;
        }

        if (resource is ISolidColorBrush b)
        {
            var sc = b.Color;
            color = new SKColor(sc.R, sc.G, sc.B, sc.A);
            return true;
        }

        color = default;
        return false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BackgroundProperty)
        {
            if (Background is ISolidColorBrush scb)
            {
                _backgroundColor = new SKColor(scb.Color.R, scb.Color.G, scb.Color.B, scb.Color.A);
                InvalidateVisual();
            }
            else
            {
                UpdateThemePaints();
            }
        }

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
            InvalidateVisual();
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        double scale = topLevel != null && topLevel.RenderScaling > 0 ? topLevel.RenderScaling : 1.0;

        int pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));

        if (_bitmap == null || _bitmap.PixelSize.Width != pixelWidth || _bitmap.PixelSize.Height != pixelHeight)
        {
            var oldBitmap = _bitmap;
            _bitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scale, 96 * scale),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
            oldBitmap?.Dispose();
        }

        using (var fb = _bitmap.Lock())
        {
            var info = new SKImageInfo(pixelWidth, pixelHeight, fb.Format.ToSkColorType(), SKAlphaType.Premul);
            var properties = new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal);
            using var surface = SKSurface.Create(info, fb.Address, fb.RowBytes, properties);
            if (surface != null)
            {
                var canvas = surface.Canvas;
                canvas.Scale((float)scale);
                DrawCanvas(canvas, (float)bounds.Width, (float)bounds.Height);
            }
        }

        context.DrawImage(_bitmap, new Rect(0, 0, bounds.Width, bounds.Height));
    }

    private void DrawCanvas(SKCanvas canvas, float width, float height)
    {
        canvas.Clear(_backgroundColor);

        DrawAdaptiveGrid(canvas, width, height);

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

            // Корректируем толщину линий контуров интерфейса, чтобы они не раздувались при зуме
            bool isFixedPixelOutline = primitive.Type is PrimitiveType.ComponentOutline
                or PrimitiveType.BoardOutline
                or PrimitiveType.Footprint
                or PrimitiveType.Net
                or PrimitiveType.Pin;

            if (isFixedPixelOutline)
            {
                float basePixelWidth = primitive switch
                {
                    LinePrimitive line when line.Thickness > 0 => line.Thickness,
                    _ => primitive.Type switch
                    {
                        PrimitiveType.BoardOutline => 2.0f,
                        PrimitiveType.Net => 1.0f,
                        PrimitiveType.Pin => 1.0f,
                        _ => 1.2f
                    }
                };

                paint.StrokeWidth = basePixelWidth / totalScale;
            }
            else if (primitive is LinePrimitive line)
            {
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

                        if (primitive.Type == PrimitiveType.Trace)
                        {
                            float traceWidth = polyline.Thickness > 0 ? polyline.Thickness : 0.25f;
                            float cornerRadius = traceWidth * 1.5f;

                            using var cornerEffect = SKPathEffect.CreateCorner(cornerRadius);
                            paint.StrokeWidth = traceWidth;
                            paint.StrokeJoin = SKStrokeJoin.Round;
                            paint.PathEffect = cornerEffect;

                            canvas.DrawPath(path, paint);
                            paint.PathEffect = null;
                        }
                        else
                        {
                            canvas.DrawPath(path, paint);
                        }
                    }

                    break;
                }

                case CirclePrimitive circle:
                    canvas.DrawCircle(circle.X, circle.Y, circle.Radius, paint);
                    break;

                case RectanglePrimitive rect:
                    var skRect = new SKRect(
                        rect.X - rect.Width / 2f,
                        rect.Y - rect.Height / 2f,
                        rect.X + rect.Width / 2f,
                        rect.Y + rect.Height / 2f);

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
                            case Quartz.Core.Models.BoardEntities.ArcSegment arc:
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

    private void DrawAdaptiveGrid(SKCanvas canvas, float width, float height)
    {
        float scale = BasePixelsPerMm * Zoom;
        float d = CalculateGridStep(scale, targetVisualPixels: 40f);

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
        dotPaint.Color = _gridColor;
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
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.ActualThemeVariantChanged -= OnGlobalThemeChanged;
        }

        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;

        foreach (var paint in _paintCache.Values)
            paint.Dispose();
        _paintCache.Clear();

        _bitmap?.Dispose();
        _bitmap = null;
    }
}