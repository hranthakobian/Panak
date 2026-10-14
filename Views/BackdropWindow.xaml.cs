using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Panak.Services;

namespace Panak.Views {
    public partial class BackdropWindow : Window {
        public event Action? ClickedOutside;

        private DispatcherTimer? _realtimeBlurTimer;
        private Rect _currentScreenBounds;
        private bool _blurEnabled = false;
        private double _blurRadius = 28.0;
        private bool _isUpdatingBlur = false;

        public BackdropWindow() {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            try {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                IntPtr exStyle = ShellHelper.GetWindowLong(hwnd, ShellHelper.GWL_EXSTYLE);
                ShellHelper.SetWindowLong(hwnd, ShellHelper.GWL_EXSTYLE, (IntPtr)(exStyle.ToInt64() | ShellHelper.WS_EX_NOACTIVATE));
                if (ShellHelper.IsWindows11OrGreater) {
                    ShellHelper.SetWindowDisplayAffinity(hwnd, ShellHelper.WDA_EXCLUDEFROMCAPTURE);
                }
            } catch { }
        }

        public void ShowBackdrop(Rect screenBounds, double targetDimmingOpacity = 0.40, bool blurEnabled = true, double blurRadius = 28.0) {
            _currentScreenBounds = screenBounds;
            _blurEnabled = blurEnabled;
            _blurRadius = blurRadius;

            double dpi = 1.0;
            try {
                dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            } catch { }
            if (dpi <= 0) dpi = 1.0;

            Left = screenBounds.Left / dpi;
            Top = screenBounds.Top / dpi;
            Width = screenBounds.Width / dpi;
            Height = screenBounds.Height / dpi;

            if (blurEnabled && blurRadius > 0) {
                double radius = Math.Clamp(blurRadius, 1.0, 100.0);
                bool isHwAccelerated = (RenderCapability.Tier >> 16) >= 2;
                if (!isHwAccelerated) {
                    radius = Math.Min(radius, 14.0);
                }
                BackdropBlurEffect.Radius = radius;
                BlurImage.Effect = BackdropBlurEffect;

                UpdateBackdropFrame();
                StartRealtimeBlurTimer();
            } else {
                StopRealtimeBlurTimer();
                BlurImage.Source = null;
                BlurImage.Visibility = Visibility.Collapsed;
            }

            DimOverlay.Opacity = Math.Clamp(targetDimmingOpacity, 0.0, 1.0);

            Show();

            DoubleAnimation fadeIn = new DoubleAnimation {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BackdropGrid.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void StartRealtimeBlurTimer() {
            StopRealtimeBlurTimer();
            if (!_blurEnabled || _blurRadius <= 0) return;
            // On Windows 10 without hardware exclusion, do not continuously re-capture over the dimmed window
            if (!ShellHelper.IsWindows11OrGreater && !ShellHelper.IsLiveWallpaperEngineRunning()) {
                return;
            }

            int intervalMs = ShellHelper.IsLiveWallpaperEngineRunning() ? 60 : 100;
            _realtimeBlurTimer = new DispatcherTimer(DispatcherPriority.Background) {
                Interval = TimeSpan.FromMilliseconds(intervalMs)
            };
            _realtimeBlurTimer.Tick += (s, e) => UpdateBackdropFrame();
            _realtimeBlurTimer.Start();
        }

        private void StopRealtimeBlurTimer() {
            if (_realtimeBlurTimer != null) {
                try {
                    _realtimeBlurTimer.Stop();
                } catch { }
                _realtimeBlurTimer = null;
            }
        }

        private void UpdateBackdropFrame() {
            if (!IsVisible && BackdropGrid.Opacity <= 0) return;
            if (_isUpdatingBlur) return;
            _isUpdatingBlur = true;

            try {
                if (ShellHelper.IsLiveWallpaperEngineRunning()) {
                    var liveBmp = ShellHelper.CaptureLiveWallpaper();
                    if (liveBmp != null) {
                        BlurImage.Source = liveBmp;
                        BlurImage.Visibility = Visibility.Visible;
                        return;
                    }
                }

                var screenBmp = ShellHelper.CaptureScreenArea(
                    (int)Math.Round(_currentScreenBounds.Left),
                    (int)Math.Round(_currentScreenBounds.Top),
                    (int)Math.Round(_currentScreenBounds.Width),
                    (int)Math.Round(_currentScreenBounds.Height),
                    dpiScale: 1.0,
                    downsampleForBlur: true);

                if (screenBmp == null && !ShellHelper.IsWindows11OrGreater) {
                    screenBmp = ShellHelper.GetDesktopWallpaperBitmap();
                }

                if (screenBmp != null) {
                    BlurImage.Source = screenBmp;
                    BlurImage.Visibility = Visibility.Visible;
                }
            } catch {
                // Fallback to desktop wallpaper on error
                try {
                    var fallback = ShellHelper.GetDesktopWallpaperBitmap();
                    if (fallback != null) {
                        BlurImage.Source = fallback;
                        BlurImage.Visibility = Visibility.Visible;
                    }
                } catch { }
            } finally {
                _isUpdatingBlur = false;
            }
        }

        public void HideBackdrop(Action? onCompleted = null) {
            StopRealtimeBlurTimer();
            DoubleAnimation fadeOut = new DoubleAnimation {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, e) => {
                Hide();
                BlurImage.Source = null;
                BlurImage.Effect = null;
                onCompleted?.Invoke();
            };
            BackdropGrid.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void BackdropGrid_MouseDown(object sender, MouseButtonEventArgs e) {
            ClickedOutside?.Invoke();
        }
    }
}
