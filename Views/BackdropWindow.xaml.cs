using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Panak.Services;

namespace Panak.Views {
    public partial class BackdropWindow : Window {
        public event Action? ClickedOutside;

        public BackdropWindow() {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr exStyle = ShellHelper.GetWindowLong(hwnd, ShellHelper.GWL_EXSTYLE);
            ShellHelper.SetWindowLong(hwnd, ShellHelper.GWL_EXSTYLE, (IntPtr)(exStyle.ToInt64() | ShellHelper.WS_EX_NOACTIVATE));
        }

        public void ShowBackdrop(Rect screenBounds, double targetDimmingOpacity = 0.40, bool blurEnabled = true, double blurRadius = 28.0) {
            Left = screenBounds.Left;
            Top = screenBounds.Top;
            Width = screenBounds.Width;
            Height = screenBounds.Height;

            try {
                double dpi = 1.0;
                try {
                    dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                } catch { }
                if (dpi <= 0) dpi = 1.0;

                var screenBmp = ShellHelper.CaptureScreenArea(
                    (int)screenBounds.Left,
                    (int)screenBounds.Top,
                    (int)screenBounds.Width,
                    (int)screenBounds.Height,
                    dpi);

                if (screenBmp != null) {
                    BlurImage.Source = screenBmp;
                    BlurImage.Visibility = Visibility.Visible;
                    if (blurEnabled && blurRadius > 0) {
                        BackdropBlurEffect.Radius = Math.Clamp(blurRadius, 1.0, 100.0);
                        BlurImage.Effect = BackdropBlurEffect;
                    } else {
                        BlurImage.Effect = null;
                    }
                } else {
                    BlurImage.Source = null;
                    BlurImage.Visibility = Visibility.Collapsed;
                }
            } catch {
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

        public void HideBackdrop(Action? onCompleted = null) {
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
