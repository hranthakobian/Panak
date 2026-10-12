using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Panak.Services;

namespace Panak.Views {
    public partial class WelcomeWindow : Window {
        public WelcomeWindow() {
            InitializeComponent();
            LoadLogo();
            LoadThemeAndLanguage();
        }

        private void LoadLogo() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Header & Window Icon: Frame 121.png (Panak Square Icon)
            BitmapImage? iconImage = null;
            try {
                iconImage = new BitmapImage(new Uri("pack://application:,,,/Frame 121.png", UriKind.Absolute));
            } catch {
                string[] iconCandidates = {
                    Path.Combine(baseDir, "src", "Frame 121.png"),
                    Path.Combine(baseDir, "Frame 121.png"),
                    Path.Combine(baseDir, "PanakSetupLogo.png"),
                    Path.Combine(baseDir, "src", "PanakSetupLogo.png"),
                    Path.Combine(baseDir, "PanakLogo.png"),
                    Path.Combine(baseDir, "Panak.ico")
                };
                foreach (var path in iconCandidates) {
                    if (File.Exists(path)) {
                        iconImage = new BitmapImage(new Uri(path, UriKind.Absolute));
                        break;
                    }
                }
            }

            if (iconImage != null) {
                HeaderLogoImage.Source = iconImage;
                Icon = iconImage;
            }

            // 2. Center Hero Logo: Group 389.png (Panak Brand Logo Banner)
            BitmapImage? bannerImage = null;
            try {
                bannerImage = new BitmapImage(new Uri("pack://application:,,,/Group 389.png", UriKind.Absolute));
            } catch {
                string[] bannerCandidates = {
                    Path.Combine(baseDir, "src", "Group 389.png"),
                    Path.Combine(baseDir, "Group 389.png"),
                    Path.Combine(baseDir, "src", "Panak logo.png"),
                    Path.Combine(baseDir, "Panak logo.png"),
                    Path.Combine(baseDir, "PanakLogo.png")
                };
                foreach (var path in bannerCandidates) {
                    if (File.Exists(path)) {
                        bannerImage = new BitmapImage(new Uri(path, UriKind.Absolute));
                        break;
                    }
                }
            }

            if (bannerImage != null) {
                CenterLogoImage.Source = bannerImage;
            } else if (iconImage != null) {
                CenterLogoImage.Source = iconImage;
            }
        }

        private void LoadThemeAndLanguage() {
            var loc = LocalizationService.Instance;
            var settings = SettingsService.Instance.Current;

            Title = loc["WelcomeToPanak"];
            WelcomeTitleText.Text = loc["WelcomeToPanak"];
            WelcomeSubtitleText.Text = loc["WelcomeSubtitle"];
            Feature1Text.Text = loc["WelcomeFeature1"];
            Feature2Text.Text = loc["WelcomeFeature2"];
            Feature3Text.Text = loc["WelcomeFeature3"];
            SettingsBtn.Content = "⚙️ " + loc["GoToSettings"];
            KofiBtn.Content = loc["DonateOnKofi"];

            string color = string.IsNullOrEmpty(settings?.ThemeColor)
                ? ShellHelper.GetDefaultPastelColorHex()
                : settings.ThemeColor;
            double hue = ShellHelper.GetHueFromHex(color);
            ApplyMaterialYouPalette(hue);
        }

        public void ApplyMaterialYouPalette(double hue) {
            try {
                hue = (hue % 360.0 + 360.0) % 360.0;

                var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);
                var trackInactiveColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.84);

                Color toWpf(System.Drawing.Color c) =>
                    Color.FromArgb(c.A, c.R, c.G, c.B);

                var wpfSurface = toWpf(surfaceColor);
                var wpfContainer = toWpf(containerColor);
                var wpfPrimary = toWpf(primaryColor);
                var wpfCard = toWpf(cardColor);
                var wpfOnSurface = toWpf(onSurfaceColor);
                var wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                var wpfTrackInactive = toWpf(trackInactiveColor);

                Resources["M3SurfaceBrush"] = new SolidColorBrush(wpfSurface);
                Resources["M3SurfaceContainerBrush"] = new SolidColorBrush(wpfContainer);
                Resources["M3CardBrush"] = new SolidColorBrush(wpfCard);
                Resources["M3PrimaryBrush"] = new SolidColorBrush(wpfPrimary);
                Resources["M3OnPrimaryBrush"] = new SolidColorBrush(Colors.White);
                Resources["M3OnSurfaceBrush"] = new SolidColorBrush(wpfOnSurface);
                Resources["M3OnSurfaceVariantBrush"] = new SolidColorBrush(wpfOnSurfaceVariant);
                Resources["M3TrackInactiveBrush"] = new SolidColorBrush(wpfTrackInactive);
            } catch { }
        }

        private void KofiBtn_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/hranthakobian/") { UseShellExecute = true });
            } catch { }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e) {
            try {
                if (Application.Current is App app) {
                    app.ShowSettings();
                }
            } catch { }
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                Close();
            }
        }
    }
}
