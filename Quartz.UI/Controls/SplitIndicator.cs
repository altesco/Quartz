using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Quartz.UI.Enums;

namespace Quartz.UI.Controls
{
    public class SplitIndicator : Control
    {
        public static readonly StyledProperty<Side> SideProperty =
            AvaloniaProperty.Register<SplitIndicator, Side>(nameof(Side), Side.None);

        public Side Side
        {
            get => GetValue(SideProperty);
            set => SetValue(SideProperty, value);
        }

        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            AvaloniaProperty.Register<SplitIndicator, IBrush?>(nameof(Background));

        public IBrush? Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
            AvaloniaProperty.Register<SplitIndicator, CornerRadius>(nameof(CornerRadius), new CornerRadius(4));

        public CornerRadius CornerRadius
        {
            get => GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly StyledProperty<IBrush?> BorderBrushProperty =
            AvaloniaProperty.Register<SplitIndicator, IBrush?>(nameof(BorderBrush));

        public IBrush? BorderBrush
        {
            get => GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public static readonly StyledProperty<double> BorderThicknessProperty =
            AvaloniaProperty.Register<SplitIndicator, double>(nameof(BorderThickness), 0.0);

        public double BorderThickness
        {
            get => GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        private const double AnimationDurationMs = 90.0;

        private Side _lastActiveSide = Side.None;
        private Rect _currentRect;
        private Rect _fromRect;
        private Rect _targetRect;
        private readonly Stopwatch _stopwatch = new();
        private bool _isTransitionActive;
        private readonly DispatcherTimer _timer;

        public SplitIndicator()
        {
            IsHitTestVisible = false;
            IsVisible = false;
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(8), DispatcherPriority.Render, OnTimerTick);
        }

        static SplitIndicator()
        {
            MarginProperty.OverrideDefaultValue<SplitIndicator>(new Thickness(10));
            AffectsRender<SplitIndicator>(BackgroundProperty, CornerRadiusProperty, BorderBrushProperty, BorderThicknessProperty);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (Side != Side.None)
            {
                IsVisible = true;
                if (Bounds.Width > 0 && Bounds.Height > 0)
                {
                    _currentRect = GetTargetRect(Side, Bounds.Width, Bounds.Height);
                    _lastActiveSide = Side;
                    InvalidateVisual();
                }
            }
            else
            {
                IsVisible = false;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _timer.Stop();
            _stopwatch.Stop();
            _isTransitionActive = false;
            IsVisible = false;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SideProperty)
            {
                var oldSide = change.GetOldValue<Side>();
                var newSide = change.GetNewValue<Side>();
                if (oldSide != newSide)
                {
                    HandleSideChanged(oldSide, newSide);
                }
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 1 || h < 1) return;

            if (!_isTransitionActive && Side != Side.None)
            {
                _currentRect = GetTargetRect(Side, w, h);
                _lastActiveSide = Side;
                InvalidateVisual();
            }
            else if (_isTransitionActive)
            {
                Side targetSide = Side != Side.None ? Side : _lastActiveSide;
                if (targetSide != Side.None)
                {
                    _targetRect = Side != Side.None ? GetTargetRect(targetSide, w, h) : GetCollapsedRect(targetSide, w, h);
                }
            }
        }

        private void HandleSideChanged(Side oldSide, Side newSide)
        {
            if (oldSide == newSide) return;

            // If we are already animating towards this exact side, don't restart animation
            if (newSide == _lastActiveSide && _isTransitionActive && Side != Side.None)
            {
                return;
            }

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 1 || h < 1)
            {
                _lastActiveSide = newSide;
                if (newSide != Side.None)
                {
                    IsVisible = true;
                }
                return;
            }

            if (newSide != Side.None)
            {
                IsVisible = true;

                if (oldSide == Side.None && !_isTransitionActive)
                {
                    // Case 1: Initial appearance -> expand from outer boundary of newSide
                    _fromRect = GetCollapsedRect(newSide, w, h);
                    _currentRect = _fromRect;
                }
                else
                {
                    // Case 2: Transition between sides (or re-entry while collapsing) -> smoothly interpolate from current position
                    _fromRect = _currentRect;
                }

                _lastActiveSide = newSide;
                _targetRect = GetTargetRect(newSide, w, h);
                StartAnimation();
            }
            else
            {
                // Case 3: Disappearing -> collapse towards outer boundary of last active side
                if (_lastActiveSide != Side.None)
                {
                    _fromRect = _currentRect;
                    _targetRect = GetCollapsedRect(_lastActiveSide, w, h);
                    StartAnimation();
                }
                else
                {
                    IsVisible = false;
                }
            }
        }

        private void StartAnimation()
        {
            _stopwatch.Restart();
            _isTransitionActive = true;
            _timer.Start();
            InvalidateVisual();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            double elapsed = _stopwatch.Elapsed.TotalMilliseconds;
            double t = Math.Clamp(elapsed / AnimationDurationMs, 0.0, 1.0);

            // Cubic ease-out: 1 - (1 - t)^3
            double ease = 1.0 - Math.Pow(1.0 - t, 3);

            _currentRect = new Rect(
                Lerp(_fromRect.X, _targetRect.X, ease),
                Lerp(_fromRect.Y, _targetRect.Y, ease),
                Math.Max(0, Lerp(_fromRect.Width, _targetRect.Width, ease)),
                Math.Max(0, Lerp(_fromRect.Height, _targetRect.Height, ease))
            );

            InvalidateVisual();

            if (t >= 1.0)
            {
                _currentRect = _targetRect;
                _isTransitionActive = false;
                _timer.Stop();
                _stopwatch.Stop();

                if (Side == Side.None)
                {
                    _lastActiveSide = Side.None;
                    IsVisible = false;
                }
                InvalidateVisual();
            }
        }

        public static Rect GetTargetRect(Side side, double w, double h)
        {
            return side switch
            {
                Side.Left => new Rect(0, 0, Math.Max(0, w / 2), Math.Max(0, h)),
                Side.Right => new Rect(w / 2, 0, Math.Max(0, w / 2), Math.Max(0, h)),
                Side.Top => new Rect(0, 0, Math.Max(0, w), Math.Max(0, h / 2)),
                Side.Bottom => new Rect(0, h / 2, Math.Max(0, w), Math.Max(0, h / 2)),
                Side.Center => new Rect(0, 0, Math.Max(0, w), Math.Max(0, h)),
                _ => default
            };
        }

        public static Rect GetCollapsedRect(Side side, double w, double h)
        {
            return side switch
            {
                Side.Left => new Rect(0, 0, 0, Math.Max(0, h)),
                Side.Right => new Rect(w, 0, 0, Math.Max(0, h)),
                Side.Top => new Rect(0, 0, Math.Max(0, w), 0),
                Side.Bottom => new Rect(0, h, Math.Max(0, w), 0),
                Side.Center => new Rect(w / 2, h / 2, 0, 0),
                _ => default
            };
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (!IsVisible || _currentRect.Width <= 0.5 || _currentRect.Height <= 0.5) return;

            var brush = Background ?? GetThemeBrush();
            if (brush == null) return;

            IPen? pen = null;
            if (BorderBrush != null && BorderThickness > 0)
            {
                pen = new Pen(BorderBrush, BorderThickness);
            }

            var cr = CornerRadius;
            context.DrawRectangle(brush, pen, new RoundedRect(_currentRect, cr.TopLeft, cr.TopRight, cr.BottomRight, cr.BottomLeft));
        }

        private IBrush? GetThemeBrush()
        {
            if (this.TryFindResource("PrimaryColor50", out var res))
            {
                if (res is IBrush b) return b;
                if (res is Color c) return new SolidColorBrush(c);
            }
            return new SolidColorBrush(Color.FromArgb(128, 137, 180, 250));
        }
    }
}
