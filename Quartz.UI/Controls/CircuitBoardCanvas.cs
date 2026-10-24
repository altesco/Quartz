using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Windows.Input;
using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Tools;
using Quartz.UI.Services;
using Quartz.UI.ViewModels;
using SkiaSharp;

namespace Quartz.UI.Controls;

public class CircuitBoardCanvas : Control, IDisposable, Avalonia.Rendering.ICustomHitTest
{
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    public static readonly StyledProperty<IReadOnlyList<DrawingPrimitive>?> DrawingDataProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, IReadOnlyList<DrawingPrimitive>?>(nameof(DrawingData));

    public IReadOnlyList<DrawingPrimitive>? DrawingData
    {
        get => GetValue(DrawingDataProperty);
        set => SetValue(DrawingDataProperty, value);
    }

    public static readonly StyledProperty<float> ZoomProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(
            nameof(Zoom),
            defaultValue: 1.0f,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<float> OffsetXProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(
            nameof(OffsetX),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<float> OffsetYProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, float>(
            nameof(OffsetY),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, IBrush?>(nameof(Background));

    public float Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetCurrentValue(ZoomProperty, value);
    }

    public float OffsetX
    {
        get => GetValue(OffsetXProperty);
        set => SetCurrentValue(OffsetXProperty, value);
    }

    public float OffsetY
    {
        get => GetValue(OffsetYProperty);
        set => SetCurrentValue(OffsetYProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly StyledProperty<LengthUnit> UnitProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, LengthUnit>(nameof(Unit), LengthUnit.Mm);

    public LengthUnit Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly StyledProperty<bool> IsOriginOffScreenProperty =
        AvaloniaProperty.Register<CircuitBoardCanvas, bool>(
            nameof(IsOriginOffScreen),
            defaultValue: false,
            defaultBindingMode: BindingMode.TwoWay);

    public bool IsOriginOffScreen
    {
        get => GetValue(IsOriginOffScreenProperty);
        set => SetCurrentValue(IsOriginOffScreenProperty, value);
    }

    public ICommand CenterOriginCommand { get; }

    private const float RulerSize = 24f;
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

    private float _pinchStartZoom;
    private float _pinchStartOffsetX;
    private float _pinchStartOffsetY;
    private Point _pinchStartOrigin;
    private bool _isPinchGestureActive;

    // Переменные для анимации плавной подгонки
    private float _targetZoom = 1.0f;
    private float _targetOffsetX;
    private float _targetOffsetY;
    private readonly DispatcherTimer _animationTimer;
    private readonly System.Diagnostics.Stopwatch _animationStopwatch = new();
    private long _lastAnimationTicks;

    private readonly Dictionary<PrimitiveType, SKPaint> _paintCache = [];
    private SKColor _backgroundColor = SKColors.Black;
    private SKColor _gridColor = new SKColor(120, 120, 120, 180);
    private SKColor _rulerBgColor = new SKColor(30, 30, 46);
    private SKColor _rulerBorderColor = new SKColor(88, 91, 112);
    private SKColor _rulerTickColor = new SKColor(127, 132, 156);
    private SKColor _rulerTextColor = new SKColor(186, 194, 222);
    private SKColor _rulerUnitTextColor = new SKColor(205, 214, 244);
    private WriteableBitmap? _bitmap;

    public CircuitBoardCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        InitializePaintCache();

        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(InputElement.PinchEvent, OnPinch);
        AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
        AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, OnPointerTouchPadGestureMagnify);

        _animationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(8),
            DispatcherPriority.Render,
            OnAnimationTick);

        CenterOriginCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CenterOrigin);
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

        LinuxX11PinchGestureService.PinchGesture -= OnLinuxPinchGesture;
        LinuxX11PinchGestureService.PinchGesture += OnLinuxPinchGesture;

        if (DataContext is CanvasVM vm)
        {
            vm.RequestCenterOrigin = CenterOrigin;
            if (!vm.IsInitiallyCentered)
            {
                EnsureInitialCentering(Bounds.Size);
            }
            else
            {
                SetCurrentValue(OffsetXProperty, vm.OffsetX);
                SetCurrentValue(OffsetYProperty, vm.OffsetY);
                SetCurrentValue(ZoomProperty, vm.Zoom);
                _targetOffsetX = vm.OffsetX;
                _targetOffsetY = vm.OffsetY;
                _targetZoom = vm.Zoom;
            }
        }

        UpdateThemePaints();
        UpdateOriginVisibility();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isPinchGestureActive = false;
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;

        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.ActualThemeVariantChanged -= OnGlobalThemeChanged;
        }

        LinuxX11PinchGestureService.PinchGesture -= OnLinuxPinchGesture;

        if (DataContext is CanvasVM vm && vm.RequestCenterOrigin == CenterOrigin)
        {
            vm.RequestCenterOrigin = null;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is CanvasVM vm)
        {
            vm.RequestCenterOrigin = CenterOrigin;
            if (!vm.IsInitiallyCentered)
            {
                EnsureInitialCentering(Bounds.Size);
            }
            else
            {
                SetCurrentValue(OffsetXProperty, vm.OffsetX);
                SetCurrentValue(OffsetYProperty, vm.OffsetY);
                SetCurrentValue(ZoomProperty, vm.Zoom);
                _targetOffsetX = vm.OffsetX;
                _targetOffsetY = vm.OffsetY;
                _targetZoom = vm.Zoom;
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        EnsureInitialCentering(e.NewSize);
        UpdateOriginVisibility();
    }

    private void EnsureInitialCentering(Size size)
    {
        if (DataContext is CanvasVM vm && !vm.IsInitiallyCentered)
        {
            if (size.Width > RulerSize && size.Height > RulerSize)
            {
                float centerX = (float)(size.Width + RulerSize) / 2f;
                float centerY = (float)(size.Height + RulerSize) / 2f;

                vm.OffsetX = centerX;
                vm.OffsetY = centerY;
                SetCurrentValue(OffsetXProperty, centerX);
                SetCurrentValue(OffsetYProperty, centerY);
                _targetOffsetX = centerX;
                _targetOffsetY = centerY;
                _targetZoom = Zoom;

                vm.IsInitiallyCentered = true;
                UpdateOriginVisibility();
            }
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

        _rulerBgColor = GetColor("BackgroundColor", "TitleBarBackgroundColor", new SKColor(30, 30, 46), targetTheme);
        _rulerBorderColor = GetColor("BorderColor", "BorderColor60", new SKColor(88, 91, 112), targetTheme);
        _rulerTickColor = GetColor("BorderColor", "MutedColor", new SKColor(127, 132, 156), targetTheme);
        _rulerTextColor = GetColor("ForegroundLeadColor", "ForegroundColor", new SKColor(186, 194, 222), targetTheme);
        _rulerUnitTextColor = GetColor("ForegroundColor", "CanvasTextColor", new SKColor(205, 214, 244), targetTheme);

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
            change.Property == OffsetYProperty ||
            change.Property == UnitProperty)
        {
            InvalidateVisual();
        }

        if (change.Property == BoundsProperty ||
            change.Property == OffsetXProperty ||
            change.Property == OffsetYProperty)
        {
            UpdateOriginVisibility();
        }
    }

    #region Input Handlers

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_isPinchGestureActive)
        {
            return;
        }

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

        if (_isPinchGestureActive)
        {
            return;
        }

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

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (!IsEffectivelyVisible) return;

        StopAnimation();

        if (!_isPinchGestureActive)
        {
            _isPinchGestureActive = true;
            _pinchStartZoom = Zoom;
            _pinchStartOffsetX = OffsetX;
            _pinchStartOffsetY = OffsetY;
            _pinchStartOrigin = e.ScaleOrigin;
        }

        if (e.Scale <= 0.001) return;

        float newZoom = Math.Clamp(_pinchStartZoom * (float)e.Scale, MinZoom, MaxZoom);
        float effectiveScale = newZoom / _pinchStartZoom;

        Point currentOrigin = e.ScaleOrigin;
        OffsetX = (float)(currentOrigin.X - (_pinchStartOrigin.X - _pinchStartOffsetX) * effectiveScale);
        OffsetY = (float)(currentOrigin.Y - (_pinchStartOrigin.Y - _pinchStartOffsetY) * effectiveScale);
        Zoom = newZoom;

        SyncTargetsWithCurrent();
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        _isPinchGestureActive = false;
        SyncTargetsWithCurrent();
        e.Handled = true;
    }

    private void OnLinuxPinchGesture(object? sender, LinuxPinchEventArgs e)
    {
        if (e.Handled || !IsEffectivelyVisible) return;

        var localPoint = e.Window.TranslatePoint(e.WindowPosition, this);

        if (!_isPinchGestureActive)
        {
            if (!localPoint.HasValue || !new Rect(Bounds.Size).Contains(localPoint.Value))
                return;
            _lastPointerPosition = localPoint.Value;
        }

        Point origin = localPoint ?? _lastPointerPosition;

        if (e.State == LinuxGestureState.Begin)
        {
            StopAnimation();
            _isPinchGestureActive = true;
            _pinchStartZoom = Zoom;
            _pinchStartOffsetX = OffsetX;
            _pinchStartOffsetY = OffsetY;
            _pinchStartOrigin = origin;
            e.Handled = true;
        }
        else if (e.State == LinuxGestureState.Update)
        {
            if (!_isPinchGestureActive)
            {
                StopAnimation();
                _isPinchGestureActive = true;
                _pinchStartZoom = Zoom;
                _pinchStartOffsetX = OffsetX;
                _pinchStartOffsetY = OffsetY;
                _pinchStartOrigin = origin;
            }

            if (e.Scale <= 0.001) return;

            float newZoom = Math.Clamp(_pinchStartZoom * (float)e.Scale, MinZoom, MaxZoom);
            float effectiveScale = newZoom / _pinchStartZoom;

            OffsetX = (float)(origin.X - (_pinchStartOrigin.X - _pinchStartOffsetX) * effectiveScale);
            OffsetY = (float)(origin.Y - (_pinchStartOrigin.Y - _pinchStartOffsetY) * effectiveScale);
            Zoom = newZoom;

            SyncTargetsWithCurrent();
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.State == LinuxGestureState.End)
        {
            _isPinchGestureActive = false;
            SyncTargetsWithCurrent();
            e.Handled = true;
        }
    }

    private void OnPointerTouchPadGestureMagnify(object? sender, PointerDeltaEventArgs e)
    {
        StopAnimation();

        var mousePos = e.GetPosition(this);
        double delta = Math.Abs(e.Delta.Y) > 0.00001 ? e.Delta.Y : e.Delta.X;
        if (Math.Abs(delta) < 0.0001) return;

        float scaleFactor = (float)Math.Exp(delta);
        float oldZoom = Zoom;
        float newZoom = Math.Clamp(Zoom * scaleFactor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.00001f) return;

        Zoom = newZoom;
        OffsetX = (float)(mousePos.X - (mousePos.X - OffsetX) * (Zoom / oldZoom));
        OffsetY = (float)(mousePos.Y - (mousePos.Y - OffsetY) * (Zoom / oldZoom));

        SyncTargetsWithCurrent();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var mousePos = e.GetPosition(this);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _isPinchGestureActive = false;
            if (Math.Abs(e.Delta.Y) < 0.0001) return;

            // Anchor world coordinate under current cursor position
            float worldX = (float)((mousePos.X - OffsetX) / Zoom);
            float worldY = (float)((mousePos.Y - OffsetY) / Zoom);

            float zoomFactor = MathF.Pow(1.2f, (float)e.Delta.Y);
            _targetZoom = Math.Clamp(_targetZoom * zoomFactor, MinZoom, MaxZoom);

            _targetOffsetX = (float)(mousePos.X - worldX * _targetZoom);
            _targetOffsetY = (float)(mousePos.Y - worldY * _targetZoom);

            if (!_animationTimer.IsEnabled)
            {
                _lastAnimationTicks = _animationStopwatch.ElapsedTicks;
                _animationStopwatch.Restart();
                _animationTimer.Start();
            }
        }
        else
        {
            StopAnimation();
            OffsetX += (float)e.Delta.X * WheelPanSensitivity;
            OffsetY += (float)e.Delta.Y * WheelPanSensitivity;
            SyncTargetsWithCurrent();
            InvalidateVisual();
        }

        e.Handled = true;
    }

    public void ZoomAt(Point center, float factor)
    {
        float worldX = (float)((center.X - OffsetX) / Zoom);
        float worldY = (float)((center.Y - OffsetY) / Zoom);

        _targetZoom = Math.Clamp(_targetZoom * factor, MinZoom, MaxZoom);
        _targetOffsetX = (float)(center.X - worldX * _targetZoom);
        _targetOffsetY = (float)(center.Y - worldY * _targetZoom);

        if (!_animationTimer.IsEnabled)
        {
            _lastAnimationTicks = _animationStopwatch.ElapsedTicks;
            _animationStopwatch.Restart();
            _animationTimer.Start();
        }
    }

    public void ResetZoom()
    {
        StopAnimation();
        Zoom = 1.0f;
        OffsetX = 0f;
        OffsetY = 0f;
        SyncTargetsWithCurrent();
        InvalidateVisual();
    }

    public void CenterOrigin()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        float targetX = (float)(Bounds.Width + RulerSize) / 2f;
        float targetY = (float)(Bounds.Height + RulerSize) / 2f;

        _targetOffsetX = targetX;
        _targetOffsetY = targetY;
        _targetZoom = Zoom;

        IsOriginOffScreen = false;

        if (!_animationTimer.IsEnabled)
        {
            _lastAnimationTicks = _animationStopwatch.ElapsedTicks;
            _animationStopwatch.Restart();
            _animationTimer.Start();
        }
    }

    private void UpdateOriginVisibility()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            IsOriginOffScreen = false;
            return;
        }

        // Coordinate (0, 0) is rendered at screen (OffsetX, OffsetY).
        // It is off-screen if it lies outside the canvas window bounds.
        bool isOffScreen = OffsetX < 0 || OffsetX > Bounds.Width ||
                           OffsetY < 0 || OffsetY > Bounds.Height;

        IsOriginOffScreen = isOffScreen;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key is Key.OemPlus or Key.Add)
            {
                var center = new Point(Bounds.Width / 2.0, Bounds.Height / 2.0);
                ZoomAt(center, 1.25f);
                e.Handled = true;
            }
            else if (e.Key is Key.OemMinus or Key.Subtract)
            {
                var center = new Point(Bounds.Width / 2.0, Bounds.Height / 2.0);
                ZoomAt(center, 1.0f / 1.25f);
                e.Handled = true;
            }
            else if (e.Key is Key.D0 or Key.NumPad0)
            {
                ResetZoom();
                e.Handled = true;
            }
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        long currentTicks = _animationStopwatch.ElapsedTicks;
        float dt = (float)(currentTicks - _lastAnimationTicks) / System.Diagnostics.Stopwatch.Frequency;
        _lastAnimationTicks = currentTicks;

        if (dt <= 0f || dt > 0.1f)
            dt = 0.016f;

        float zoomDiff = Math.Abs(_targetZoom - Zoom);
        float offsetDiff = Math.Max(Math.Abs(_targetOffsetX - OffsetX), Math.Abs(_targetOffsetY - OffsetY));

        if (zoomDiff < 0.0005f && offsetDiff < 0.05f)
        {
            Zoom = _targetZoom;
            OffsetX = _targetOffsetX;
            OffsetY = _targetOffsetY;
            _animationTimer.Stop();
            _animationStopwatch.Stop();
            InvalidateVisual();
            return;
        }

        // Frame-rate independent exponential decay: gives silky smooth 60-120+ FPS transition (~120ms total)
        float alpha = 1.0f - MathF.Exp(-22.0f * dt);

        Zoom += (_targetZoom - Zoom) * alpha;
        OffsetX += (_targetOffsetX - OffsetX) * alpha;
        OffsetY += (_targetOffsetY - OffsetY) * alpha;

        InvalidateVisual();
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
        _animationStopwatch.Stop();
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
                DrawCanvas(canvas, (float)bounds.Width, (float)bounds.Height, (float)scale);
            }
        }

        context.DrawImage(_bitmap, new Rect(0, 0, bounds.Width, bounds.Height));
    }

    private void DrawCanvas(SKCanvas canvas, float width, float height, float renderScale)
    {
        EnsureInitialCentering(new Size(width, height));
        canvas.Clear(_backgroundColor);

        canvas.Save();
        canvas.ClipRect(new SKRect(RulerSize, RulerSize, width, height));

        DrawAdaptiveGrid(canvas, width, height);

        var primitives = DrawingData;
        if (primitives != null && primitives.Count > 0)
        {
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

        canvas.Restore();

        DrawRulers(canvas, width, height, renderScale);
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
        if (width <= RulerSize || height <= RulerSize) return;

        float unitFactor = (float)(1.0).ToMillimeters(Unit);
        if (unitFactor <= 0) unitFactor = 1.0f;
        float scalePerUnit = BasePixelsPerMm * unitFactor * Zoom;
        float d = CalculateGridStep(scalePerUnit, targetVisualPixels: 40f);

        float minX = (RulerSize - OffsetX) / scalePerUnit;
        float minY = (RulerSize - OffsetY) / scalePerUnit;
        float maxX = (width - OffsetX) / scalePerUnit;
        float maxY = (height - OffsetY) / scalePerUnit;

        long startX = (long)Math.Floor(minX / d);
        long endX = (long)Math.Ceiling(maxX / d);
        long startY = (long)Math.Floor(minY / d);
        long endY = (long)Math.Ceiling(maxY / d);

        using var dotPaint = new SKPaint();
        dotPaint.Color = _gridColor;
        dotPaint.IsAntialias = true;
        dotPaint.Style = SKPaintStyle.Fill;

        float dotRadius = 1.2f;

        for (long x = startX; x <= endX; x++)
        {
            float xPx = (x * d) * scalePerUnit + OffsetX;
            for (long y = startY; y <= endY; y++)
            {
                float yPx = (y * d) * scalePerUnit + OffsetY;
                canvas.DrawCircle(xPx, yPx, dotRadius, dotPaint);
            }
        }
    }

    private static string FormatRulerNumber(double value)
    {
        if (Math.Abs(value) < 1e-6)
            return "0";

        double rounded = Math.Round(value, 6);
        if (Math.Abs(rounded) < 1e-6)
            return "0";

        return rounded.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void DrawRulers(SKCanvas canvas, float width, float height, float renderScale)
    {
        if (width <= RulerSize || height <= RulerSize) return;

        float pixelSize = 1f / (renderScale > 0 ? renderScale : 1f);
        float halfPixel = pixelSize / 2f;
        float borderPos = RulerSize - halfPixel;

        float unitFactor = (float)(1.0).ToMillimeters(Unit);
        if (unitFactor <= 0) unitFactor = 1.0f;
        float scalePerUnit = BasePixelsPerMm * unitFactor * Zoom;
        float d = CalculateGridStep(scalePerUnit, targetVisualPixels: 40f);
        float stepPx = d * scalePerUnit;

        using var bgPaint = new SKPaint
        {
            Color = _rulerBgColor,
            Style = SKPaintStyle.Fill
        };

        using var borderPaint = new SKPaint
        {
            Color = _rulerBorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = pixelSize,
            IsAntialias = false
        };

        using var tickPaint = new SKPaint
        {
            Color = _rulerTickColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = pixelSize,
            IsAntialias = false
        };

        using var textPaint = new SKPaint
        {
            Color = _rulerTextColor,
            IsAntialias = true
        };

        using var font = new SKFont(SKTypeface.Default, 9f);

        // 1. Top Ruler Background
        canvas.DrawRect(new SKRect(RulerSize, 0, width, RulerSize), bgPaint);

        // 2. Left Ruler Background
        canvas.DrawRect(new SKRect(0, RulerSize, RulerSize, height), bgPaint);

        // Determine labeling interval so numbers don't collide
        int labelInterval = 1;
        if (stepPx < 45f)
            labelInterval = stepPx < 25f ? 5 : 2;

        // 3. Draw Top Ruler Ticks and Numbers
        canvas.Save();
        canvas.ClipRect(new SKRect(RulerSize, 0, width, RulerSize));

        long startX = (long)Math.Floor((RulerSize - OffsetX) / stepPx);
        long endX = (long)Math.Ceiling((width - OffsetX) / stepPx);

        for (long x = startX; x <= endX; x++)
        {
            float xPx = (x * d) * scalePerUnit + OffsetX;
            if (xPx < RulerSize || xPx > width) continue;

            bool isMajor = (x % labelInterval == 0);
            float tickHeight = isMajor ? 7f : 4f;

            canvas.DrawLine(xPx, RulerSize - tickHeight, xPx, borderPos, tickPaint);

            if (isMajor)
            {
                string text = FormatRulerNumber(x * d);
                font.MeasureText(text, out var textBounds);
                float textX = xPx - textBounds.MidX;
                float textY = (RulerSize - 7f) / 2f - textBounds.MidY;
                canvas.DrawText(text, textX, textY, font, textPaint);
            }
        }
        canvas.Restore();

        // 4. Draw Left Ruler Ticks and Numbers
        canvas.Save();
        canvas.ClipRect(new SKRect(0, RulerSize, RulerSize, height));

        long startY = (long)Math.Floor((RulerSize - OffsetY) / stepPx);
        long endY = (long)Math.Ceiling((height - OffsetY) / stepPx);

        for (long y = startY; y <= endY; y++)
        {
            float yPx = (y * d) * scalePerUnit + OffsetY;
            if (yPx < RulerSize || yPx > height) continue;

            bool isMajor = (y % labelInterval == 0);
            float tickLength = isMajor ? 7f : 4f;

            canvas.DrawLine(RulerSize - tickLength, yPx, borderPos, yPx, tickPaint);

            if (isMajor)
            {
                string text = FormatRulerNumber(y * d);
                font.MeasureText(text, out var textBounds);

                canvas.Save();
                canvas.Translate((RulerSize - 7f) / 2f, yPx);
                canvas.RotateDegrees(-90);
                canvas.DrawText(text, -textBounds.MidX, -textBounds.MidY, font, textPaint);
                canvas.Restore();
            }
        }
        canvas.Restore();

        // 5. Corner Box (Intersection)
        canvas.DrawRect(new SKRect(0, 0, RulerSize, RulerSize), bgPaint);

        // Unit text in lowercase in corner box
        string unitStr = Unit.ToString().ToLowerInvariant();
        using var unitFont = new SKFont(SKTypeface.Default, 10f);
        using var unitTextPaint = new SKPaint
        {
            Color = _rulerUnitTextColor,
            IsAntialias = true
        };
        unitFont.MeasureText(unitStr, out var unitBounds);
        float unitX = RulerSize / 2f - unitBounds.MidX;
        float unitY = RulerSize / 2f - unitBounds.MidY;
        canvas.DrawText(unitStr, unitX, unitY, unitFont, unitTextPaint);

        // 6. Ruler Border Lines (exact 1 physical pixel width)
        // Bottom border of top ruler & corner box
        canvas.DrawLine(0, borderPos, width, borderPos, borderPaint);
        // Right border of left ruler & corner box
        canvas.DrawLine(borderPos, 0, borderPos, height, borderPaint);
    }

    public void Dispose()
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.ActualThemeVariantChanged -= OnGlobalThemeChanged;
        }

        RemoveHandler(InputElement.PinchEvent, OnPinch);
        RemoveHandler(InputElement.PinchEndedEvent, OnPinchEnded);
        RemoveHandler(InputElement.PointerTouchPadGestureMagnifyEvent, OnPointerTouchPadGestureMagnify);
        LinuxX11PinchGestureService.PinchGesture -= OnLinuxPinchGesture;

        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _animationStopwatch.Stop();

        foreach (var paint in _paintCache.Values)
            paint.Dispose();
        _paintCache.Clear();

        _bitmap?.Dispose();
        _bitmap = null;
    }
}