using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Panak.Controls {
    /// <summary>
    /// Material 3 Expressive Wavy Progress Bar Control.
    /// Draws a soft pill background track and a fluid sinusoidal active wave indicator
    /// with rounded caps, edge tapering, and continuous 60fps wave motion.
    /// </summary>
    public class WavyProgressBar : ProgressBar {
        public static readonly DependencyProperty WaveAmplitudeProperty =
            DependencyProperty.Register(nameof(WaveAmplitude), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(3.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveLengthProperty =
            DependencyProperty.Register(nameof(WaveLength), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(26.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveStrokeThicknessProperty =
            DependencyProperty.Register(nameof(WaveStrokeThickness), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(4.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackHeightProperty =
            DependencyProperty.Register(nameof(TrackHeight), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveSpeedProperty =
            DependencyProperty.Register(nameof(WaveSpeed), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(32.0, FrameworkPropertyMetadataOptions.None));

        public double WaveAmplitude {
            get => (double)GetValue(WaveAmplitudeProperty);
            set => SetValue(WaveAmplitudeProperty, value);
        }

        public double WaveLength {
            get => (double)GetValue(WaveLengthProperty);
            set => SetValue(WaveLengthProperty, value);
        }

        public double WaveStrokeThickness {
            get => (double)GetValue(WaveStrokeThicknessProperty);
            set => SetValue(WaveStrokeThicknessProperty, value);
        }

        public double TrackHeight {
            get => (double)GetValue(TrackHeightProperty);
            set => SetValue(TrackHeightProperty, value);
        }

        public double WaveSpeed {
            get => (double)GetValue(WaveSpeedProperty);
            set => SetValue(WaveSpeedProperty, value);
        }

        private double _phase = 0.0;
        private double _indeterminateOffset = 0.0;
        private bool _isIndeterminateForward = true;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private long _lastTicks = 0;
        private bool _isHooked = false;

        static WavyProgressBar() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WavyProgressBar), new FrameworkPropertyMetadata(typeof(WavyProgressBar)));
        }

        public WavyProgressBar() {
            Height = 16;
            Background = null;
            Foreground = null;

            Loaded += (s, e) => UpdateAnimationState();
            Unloaded += (s, e) => UpdateAnimationState();
            IsVisibleChanged += (s, e) => UpdateAnimationState();
        }

        private void UpdateAnimationState() {
            bool shouldAnimate = IsLoaded && IsVisible;
            if (shouldAnimate && !_isHooked) {
                _stopwatch.Restart();
                _lastTicks = _stopwatch.ElapsedTicks;
                CompositionTarget.Rendering += OnRendering;
                _isHooked = true;
            } else if (!shouldAnimate && _isHooked) {
                CompositionTarget.Rendering -= OnRendering;
                _isHooked = false;
                _stopwatch.Stop();
            }
        }

        private void OnRendering(object? sender, EventArgs e) {
            if (!IsLoaded || !IsVisible) {
                UpdateAnimationState();
                return;
            }

            long currentTicks = _stopwatch.ElapsedTicks;
            double dt = (double)(currentTicks - _lastTicks) / Stopwatch.Frequency;
            _lastTicks = currentTicks;

            if (dt <= 0 || dt > 0.1) {
                dt = 0.016;
            }

            double speed = WaveSpeed;
            double wl = Math.Max(10.0, WaveLength);
            _phase = (_phase + dt * speed * (2.0 * Math.PI / wl)) % (2.0 * Math.PI);

            if (IsIndeterminate) {
                double travelSpeed = ActualWidth > 0 ? (ActualWidth * 0.6) : 100.0;
                double maxTravel = Math.Max(0.0, ActualWidth - (ActualWidth * 0.35));
                if (maxTravel > 0) {
                    if (_isIndeterminateForward) {
                        _indeterminateOffset += dt * travelSpeed;
                        if (_indeterminateOffset >= maxTravel) {
                            _indeterminateOffset = maxTravel;
                            _isIndeterminateForward = false;
                        }
                    } else {
                        _indeterminateOffset -= dt * travelSpeed;
                        if (_indeterminateOffset <= 0) {
                            _indeterminateOffset = 0;
                            _isIndeterminateForward = true;
                        }
                    }
                }
            }

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc) {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0) {
                return;
            }

            double centerY = height / 2.0;
            double trackH = Math.Max(2.0, TrackHeight);
            double trackRadius = trackH / 2.0;

            // 1. Inactive Track (M3 Expressive pill)
            Brush trackBrush = Background
                ?? (TryFindResource("M3TrackInactiveBrush") as Brush)
                ?? new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));

            dc.DrawRoundedRectangle(trackBrush, null, new Rect(0, centerY - trackH / 2.0, width, trackH), trackRadius, trackRadius);

            // 2. Active Wave Indicator
            Brush activeBrush = Foreground
                ?? (TryFindResource("M3PrimaryBrush") as Brush)
                ?? new SolidColorBrush(Color.FromRgb(0x7A, 0x54, 0x27));

            double strokeW = Math.Max(2.0, WaveStrokeThickness);
            var wavePen = new Pen(activeBrush, strokeW) {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            wavePen.Freeze();

            double amp = WaveAmplitude;
            double wl = Math.Max(10.0, WaveLength);

            if (IsIndeterminate) {
                double segW = Math.Max(20.0, width * 0.35);
                double startX = _indeterminateOffset;
                double endX = Math.Min(width, startX + segW);

                DrawWavySegment(dc, wavePen, startX, endX, centerY, amp, wl, _phase);
            } else {
                double range = Maximum - Minimum;
                double fraction = range > 0 ? Math.Clamp((Value - Minimum) / range, 0.0, 1.0) : 0.0;
                double activeW = width * fraction;

                if (activeW > 1.5) {
                    DrawWavySegment(dc, wavePen, 0.0, activeW, centerY, amp, wl, _phase);
                }
            }
        }

        private static void DrawWavySegment(
            DrawingContext dc,
            Pen pen,
            double startX,
            double endX,
            double centerY,
            double amplitude,
            double wavelength,
            double phase) {

            double length = endX - startX;
            if (length <= 1.0) {
                return;
            }

            if (length < 8.0) {
                // Short dot/pill
                dc.DrawLine(pen, new Point(startX, centerY), new Point(endX, centerY));
                return;
            }

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open()) {
                ctx.BeginFigure(new Point(startX, centerY), isFilled: false, isClosed: false);

                double step = 2.0;
                double taperDist = Math.Min(12.0, length * 0.25);

                for (double x = startX + step; x < endX; x += step) {
                    double distFromStart = x - startX;
                    double distFromEnd = endX - x;

                    // Smooth edge taper so wave begins and ends cleanly on the center baseline
                    double taper = Math.Min(1.0, distFromStart / taperDist) * Math.Min(1.0, distFromEnd / taperDist);
                    // Smooth sinusoidal easing
                    taper = Math.Sin(taper * Math.PI * 0.5);

                    double y = centerY + amplitude * taper * Math.Sin((2.0 * Math.PI * (x - startX) / wavelength) - phase);
                    ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
                }

                ctx.LineTo(new Point(endX, centerY), isStroked: true, isSmoothJoin: true);
            }
            geometry.Freeze();

            dc.DrawGeometry(null, pen, geometry);
        }
    }
}
