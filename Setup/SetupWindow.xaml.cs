using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Panak.Services;

namespace Panak.Setup {
    public partial class SetupWindow : Window {
        private readonly bool _isUninstallMode;
        private string _targetDirectory;
        private bool _isUpdatingLanguage = false;
        private bool _needsAutoDetect = false;

        public SetupWindow(bool isUninstallMode = false) {
            InitializeComponent();
            _isUninstallMode = isUninstallMode;

            _targetDirectory = StandaloneInstallerService.GetInstalledDirectory();
            if (string.IsNullOrEmpty(_targetDirectory)) {
                _targetDirectory = StandaloneInstallerService.GetDefaultInstallDirectory();
            }

            LoadLogoAndIcon();
            LoadThemeAndLanguage();
            ConfigureMode();

            Loaded += SetupWindow_Loaded;
        }

        private async void SetupWindow_Loaded(object sender, RoutedEventArgs e) {
            if (_needsAutoDetect) {
                string detected = await LocalizationService.DetectOptimalLanguageAsync();
                SetSelectedLanguage(detected);
            }
        }

        private void LoadLogoAndIcon() {
            try {
                var asm = typeof(SetupWindow).Assembly;
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("PanakSetupLogoEmbedded.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("PanakSetupLogo.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("Panak setup logo.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("PanakLogoEmbedded.png", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(resName)) {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null) {
                        var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        ms.Position = 0;

                        var bi = new System.Windows.Media.Imaging.BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.EndInit();
                        bi.Freeze();

                        HeaderLogoImage.Source = bi;
                        Icon = bi;
                        return;
                    }
                }
            } catch { }

            try {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(exeDir, "PanakSetupLogo.png"),
                    Path.Combine(exeDir, "Panak setup logo.png"),
                    Path.Combine(exeDir, "PanakLogo.png"),
                    Path.Combine(exeDir, "Panak logo.png")
                };
                foreach (var localPng in candidates) {
                    if (File.Exists(localPng)) {
                        var bi = new System.Windows.Media.Imaging.BitmapImage(new Uri(localPng, UriKind.Absolute));
                        HeaderLogoImage.Source = bi;
                        Icon = bi;
                        return;
                    }
                }
            } catch { }
        }

        private void LoadThemeAndLanguage() {
            string language = "hy";
            string colorHex = ShellHelper.GetDefaultPastelColorHex();
            bool foundSavedLanguage = false;

            try {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string settingsPath = Path.Combine(appData, "Panak", "settings.json");
                if (File.Exists(settingsPath)) {
                    string json = File.ReadAllText(settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var langProp) && !string.IsNullOrEmpty(langProp.GetString())) {
                        language = langProp.GetString()!;
                        foundSavedLanguage = true;
                    }
                    if (doc.RootElement.TryGetProperty("ThemeColor", out var colProp) && !string.IsNullOrEmpty(colProp.GetString())) {
                        colorHex = colProp.GetString()!;
                    }
                }
            } catch { }

            _needsAutoDetect = !foundSavedLanguage;
            SetSelectedLanguage(language);

            double hue = ShellHelper.GetHueFromHex(colorHex);
            ApplyMaterialYouPalette(hue);
        }

        private void SetSelectedLanguage(string lang) {
            _isUpdatingLanguage = true;
            try {
                foreach (System.Windows.Controls.ComboBoxItem item in LanguageComboBox.Items) {
                    if (string.Equals(item.Tag?.ToString(), lang, StringComparison.OrdinalIgnoreCase)) {
                        item.IsSelected = true;
                        break;
                    }
                }
                LocalizationService.Instance.CurrentLanguage = lang;
            } finally {
                _isUpdatingLanguage = false;
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (_isUpdatingLanguage) return;
            if (LanguageComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string lang) {
                LocalizationService.Instance.CurrentLanguage = lang;
                ConfigureMode();
            }
        }

        public void ApplyMaterialYouPalette(double hue) {
            try {
                hue = (hue % 360.0 + 360.0) % 360.0;

                var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);
                var trackInactiveColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.84);

                Color toWpf(System.Drawing.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);

                var wpfSurface = toWpf(surfaceColor);
                var wpfContainer = toWpf(containerColor);
                var wpfCard = toWpf(cardColor);
                var wpfPrimary = toWpf(primaryColor);
                var wpfOnSurface = toWpf(onSurfaceColor);
                var wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                var wpfTrackInactive = toWpf(trackInactiveColor);
                var wpfOnPrimary = Color.FromRgb(0xFE, 0xF4, 0xEB);

                Resources["M3SurfaceBrush"] = new SolidColorBrush(wpfSurface);
                Resources["M3SurfaceContainerBrush"] = new SolidColorBrush(wpfContainer);
                Resources["M3CardBrush"] = new SolidColorBrush(wpfCard);
                Resources["M3PrimaryBrush"] = new SolidColorBrush(wpfPrimary);
                Resources["M3OnPrimaryBrush"] = new SolidColorBrush(wpfOnPrimary);
                Resources["M3OnSurfaceBrush"] = new SolidColorBrush(wpfOnSurface);
                Resources["M3OnSurfaceVariantBrush"] = new SolidColorBrush(wpfOnSurfaceVariant);
                Resources["M3TrackInactiveBrush"] = new SolidColorBrush(wpfTrackInactive);

                if (MainWindowBorder != null) {
                    MainWindowBorder.Background = new SolidColorBrush(wpfSurface);
                }
                if (LanguageComboBox != null) {
                    LanguageComboBox.Foreground = new SolidColorBrush(wpfOnSurface);
                }
            } catch { }
        }

        private void ConfigureMode() {
            var loc = LocalizationService.Instance;

            InstallFolderDisplayPath.Text = _targetDirectory;

            if (_isUninstallMode) {
                Title = loc["UninstallerTitle"];
                WizardTitleText.Text = loc["UninstallerTitle"];
                WizardSubtitleText.Text = loc["UninstallDesc"];
                InstallPanel.Visibility = Visibility.Collapsed;
                UninstallPanel.Visibility = Visibility.Visible;
                MainActionBtn.Content = loc["Uninstall"];
                MainActionBtn.Style = (Style)FindResource("DangerButtonStyle");
            } else {
                Title = loc["InstallerTitle"];
                WizardTitleText.Text = loc["InstallerTitle"];
                WizardSubtitleText.Text = loc["InstallDesc"];
                InstallPanel.Visibility = Visibility.Visible;
                UninstallPanel.Visibility = Visibility.Collapsed;
                MainActionBtn.Content = loc["Install"];
                MainActionBtn.Style = (Style)FindResource("ActionButtonStyle");
            }

            CancelActionBtn.Content = loc["Cancel"];
            FinishBtn.Content = loc["Finish"];

            InstallIntroText.Text = loc["InstallIntroDesc"];
            InstallFolderTitleText.Text = GetLocalizedInstallFolderTitle();
            BrowseFolderBtn.Content = GetLocalizedBrowseText();

            DesktopShortcutTitleText.Text = loc["DesktopShortcut"];
            DesktopShortcutDescText.Text = loc["DesktopShortcutDesc"];
            StartMenuShortcutTitleText.Text = loc["StartMenuShortcut"];
            StartMenuShortcutDescText.Text = loc["StartMenuShortcutDesc"];
            AutoStartTitleText.Text = loc["AutoStart"];
            AutoStartDescText.Text = loc["AutoStartDesc"];

            UninstallIntroText.Text = loc["UninstallConfirm"];
            UninstallWillRemoveText.Text = loc["UninstallWillRemove"];
            UninstallItemContextMenuText.Text = "• " + loc["UninstallItemContextMenu"];
            UninstallItemShortcutsText.Text = "• " + loc["UninstallItemShortcuts"];
            UninstallItemStartupText.Text = "• " + loc["UninstallItemStartup"];
            UninstallItemFilesText.Text = "• " + loc["UninstallItemFiles"];
        }

        private string GetLocalizedInstallFolderTitle() {
            return LocalizationService.Instance.CurrentLanguage switch {
                "hy" => "Տեղադրման թղթապանակ",
                "ru" => "Папка установки",
                _ => "Installation Folder"
            };
        }

        private string GetLocalizedBrowseText() {
            return LocalizationService.Instance.CurrentLanguage switch {
                "hy" => "Ընտրել...",
                "ru" => "Обзор...",
                _ => "Browse..."
            };
        }

        private void BrowseFolderBtn_Click(object sender, RoutedEventArgs e) {
            try {
                var dialog = new Microsoft.Win32.OpenFolderDialog {
                    Title = GetLocalizedInstallFolderTitle(),
                    InitialDirectory = _targetDirectory
                };

                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName)) {
                    _targetDirectory = dialog.FolderName;
                    InstallFolderDisplayPath.Text = _targetDirectory;
                }
            } catch { }
        }

        private async void MainActionBtn_Click(object sender, RoutedEventArgs e) {
            var loc = LocalizationService.Instance;

            // Transition to progress panel
            InstallPanel.Visibility = Visibility.Collapsed;
            UninstallPanel.Visibility = Visibility.Collapsed;
            MainActionBtn.Visibility = Visibility.Collapsed;
            CancelActionBtn.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;

            ProgressTitleText.Text = _isUninstallMode ? loc["Uninstall"] : loc["Install"];
            InstallationProgressBar.Value = 10;
            ProgressStatusText.Text = _isUninstallMode ? "Preparing uninstallation..." : "Preparing installation...";

            var progress = new Progress<(double percent, string status)>(val => {
                InstallationProgressBar.Value = Math.Clamp(val.percent * 100.0, 0, 100);
                ProgressStatusText.Text = val.status;
            });

            if (_isUninstallMode) {
                var (success, error) = await StandaloneInstallerService.UninstallAsync(_targetDirectory, progress);
                ShowCompleted(success, loc["UninstalledSuccess"], !string.IsNullOrEmpty(error) ? error : "Uninstallation failed.");
            } else {
                bool desktop = DesktopShortcutCheckBox.IsChecked ?? true;
                bool startMenu = StartMenuShortcutCheckBox.IsChecked ?? true;
                bool autoStart = AutoStartCheckBox.IsChecked ?? true;

                var (success, error) = await StandaloneInstallerService.InstallAsync(_targetDirectory, desktop, startMenu, autoStart, progress);
                ShowCompleted(success, loc["InstalledSuccess"], !string.IsNullOrEmpty(error) ? error : "Installation failed.");
            }
        }

        private void ShowCompleted(bool success, string successMsg, string failMsg) {
            ProgressPanel.Visibility = Visibility.Collapsed;
            CompletedPanel.Visibility = Visibility.Visible;
            FinishBtn.Visibility = Visibility.Visible;

            var loc = LocalizationService.Instance;
            if (success) {
                StatusEmoji.Text = "🎉";
                StatusTitle.Text = successMsg;
                StatusSubtitle.Text = _isUninstallMode ? loc["UninstalledSuccess"] : loc["ReadyToUse"];
            } else {
                StatusEmoji.Text = "⚠️";
                StatusTitle.Text = failMsg;
                StatusSubtitle.Text = "Please ensure no files are currently locked and try again.";
            }
        }

        private void FinishBtn_Click(object sender, RoutedEventArgs e) {
            if (!_isUninstallMode) {
                string exe = Path.Combine(_targetDirectory, "Panak.exe");
                if (File.Exists(exe)) {
                    try {
                        Process.Start(new ProcessStartInfo {
                            FileName = exe,
                            Arguments = "--background",
                            UseShellExecute = true
                        });
                    } catch { }
                }
            }
            Application.Current.Shutdown();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }
    }
}
