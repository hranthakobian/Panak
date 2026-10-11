using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Panak.Models;
using Panak.Services;

namespace Panak.Views {
    public partial class SettingsWindow : Window {
        private bool _isInitializing = true;
        private string _selectedShape = "Squircle";
        private string _selectedColor = ShellHelper.DefaultPastelColorHex;
        private string _selectedThemeMode = "Auto";

        public SettingsWindow() {
            InitializeComponent();
            LoadLogoAndIcon();
            LoadCurrentSettings();
            ApplyLocalization();
            _isInitializing = false;

            GitHubUpdateService.Instance.StatusChanged += OnUpdateStatusChanged;
            GitHubUpdateService.Instance.DownloadProgressChanged += OnDownloadProgressChanged;
            Closed += (s, e) => {
                GitHubUpdateService.Instance.StatusChanged -= OnUpdateStatusChanged;
                GitHubUpdateService.Instance.DownloadProgressChanged -= OnDownloadProgressChanged;
            };
        }

        private void LoadLogoAndIcon() {
            try {
                var uri = new Uri("pack://application:,,,/PanakLogo.png", UriKind.Absolute);
                var bi = new BitmapImage(uri);
                HeaderLogoImage.Source = bi;
                Icon = bi;
                return;
            } catch { }

            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(baseDir, "PanakLogo.png"),
                    Path.Combine(baseDir, "Panak logo.png"),
                    Path.Combine(baseDir, "Panak.ico")
                };
                foreach (var file in candidates) {
                    if (File.Exists(file)) {
                        var bi = new BitmapImage(new Uri(file, UriKind.Absolute));
                        HeaderLogoImage.Source = bi;
                        Icon = bi;
                        return;
                    }
                }
            } catch { }
        }

        public void ApplyMaterialYouPalette(double hue) {
            try {
                hue = (hue % 360.0 + 360.0) % 360.0;

                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());

                System.Windows.Media.Color toWpf(System.Drawing.Color c) =>
                    System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);

                System.Windows.Media.Color wpfSurface;
                System.Windows.Media.Color wpfContainer;
                System.Windows.Media.Color wpfCard;
                System.Windows.Media.Color wpfPrimary;
                System.Windows.Media.Color wpfOnPrimary;
                System.Windows.Media.Color wpfOnSurface;
                System.Windows.Media.Color wpfOnSurfaceVariant;
                System.Windows.Media.Color wpfTrackInactive;

                if (isDark) {
                    wpfSurface = toWpf(ShellHelper.ColorFromHsl(hue, 0.16, 0.11)); // Deep dark surface #16161D
                    wpfContainer = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.18));
                    wpfCard = toWpf(ShellHelper.ColorFromHsl(hue, 0.18, 0.14));
                    wpfPrimary = toWpf(ShellHelper.ColorFromHsl(hue, 0.70, 0.72));
                    wpfOnPrimary = System.Windows.Media.Color.FromRgb(0x1C, 0x1B, 0x1F);
                    wpfOnSurface = System.Windows.Media.Color.FromRgb(0xE6, 0xE1, 0xE5);
                    wpfOnSurfaceVariant = System.Windows.Media.Color.FromRgb(0xCA, 0xC4, 0xD0);
                    wpfTrackInactive = toWpf(ShellHelper.ColorFromHsl(hue, 0.15, 0.26));
                } else {
                    var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                    var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                    var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                    var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);
                    var trackInactiveColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.84);

                    wpfSurface = toWpf(surfaceColor);
                    wpfContainer = toWpf(containerColor);
                    wpfCard = toWpf(cardColor);
                    wpfPrimary = toWpf(primaryColor);
                    wpfOnPrimary = System.Windows.Media.Color.FromRgb(0xFE, 0xF4, 0xEB);
                    wpfOnSurface = toWpf(onSurfaceColor);
                    wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                    wpfTrackInactive = toWpf(trackInactiveColor);
                }

                // Update dynamic brushes in Resources in place so styles, templates and existing controls adapt instantly
                void SetBrush(string key, System.Windows.Media.Color col) {
                    if (Resources[key] is System.Windows.Media.SolidColorBrush scb) {
                        scb.Color = col;
                    } else {
                        Resources[key] = new System.Windows.Media.SolidColorBrush(col);
                    }
                }

                SetBrush("M3SurfaceBrush", wpfSurface);
                SetBrush("M3SurfaceContainerBrush", wpfContainer);
                SetBrush("M3CardBrush", wpfCard);
                SetBrush("M3PrimaryBrush", wpfPrimary);
                SetBrush("M3OnPrimaryBrush", wpfOnPrimary);
                SetBrush("M3OnSurfaceBrush", wpfOnSurface);
                SetBrush("M3OnSurfaceVariantBrush", wpfOnSurfaceVariant);
                SetBrush("M3TrackInactiveBrush", wpfTrackInactive);
                SetBrush("SettingsHueThumbBrush", wpfContainer);

                if (MainWindowBorder != null) {
                    MainWindowBorder.Background = new System.Windows.Media.SolidColorBrush(wpfSurface);
                }
                if (SettingsColorIndicator != null) {
                    SettingsColorIndicator.Background = new System.Windows.Media.SolidColorBrush(wpfContainer);
                }
                if (LanguageComboBox != null) {
                    LanguageComboBox.Foreground = new System.Windows.Media.SolidColorBrush(wpfOnSurface);
                }
            } catch { }
        }

        private void LoadCurrentSettings() {
            var s = SettingsService.Instance.Current;
            _selectedShape = s.IconShape;
            _selectedColor = string.IsNullOrEmpty(s.ThemeColor) ? ShellHelper.GetDefaultPastelColorHex() : s.ThemeColor;

            // Theme Mode
            _selectedThemeMode = string.IsNullOrEmpty(s.ThemeMode) ? "Auto" : s.ThemeMode;
            if (string.Equals(_selectedThemeMode, "Light", StringComparison.OrdinalIgnoreCase)) {
                SettingsThemeLight.IsChecked = true;
            } else if (string.Equals(_selectedThemeMode, "Dark", StringComparison.OrdinalIgnoreCase)) {
                SettingsThemeDark.IsChecked = true;
            } else {
                SettingsThemeAuto.IsChecked = true;
            }

            // Apply Material You real-time palette for the current color/hue
            double initialHue = ShellHelper.GetHueFromHex(_selectedColor);
            ApplyMaterialYouPalette(initialHue);

            // Language
            foreach (ComboBoxItem item in LanguageComboBox.Items) {
                if (item.Tag?.ToString() == s.Language) {
                    item.IsSelected = true;
                    break;
                }
            }

            // Shape
            if (string.Equals(s.IconShape, "Circle", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeCircle.IsChecked = true;
            } else if (string.Equals(s.IconShape, "Clover", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeClover.IsChecked = true;
            } else if (string.Equals(s.IconShape, "Scallop", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeScallop.IsChecked = true;
            } else if (string.Equals(s.IconShape, "Arch", StringComparison.OrdinalIgnoreCase) || string.Equals(s.IconShape, "Teardrop", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeArch.IsChecked = true;
            } else {
                SettingsShapeSquircle.IsChecked = true;
            }

            // Pastel Theme Color Setup
            string defaultPastel = ShellHelper.DefaultPastelColorHex;
            SettingsAccentSwatch.Tag = defaultPastel;
            try {
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(defaultPastel);
                SettingsAccentSwatch.Background = new System.Windows.Media.SolidColorBrush(c);
            } catch { }

            bool foundColorSwatch = false;
            foreach (var child in SettingsColorSwatchesPanel.Children) {
                if (child is RadioButton rb && rb.Tag is string tag) {
                    bool isMatch = string.Equals(tag, _selectedColor, StringComparison.OrdinalIgnoreCase);
                    rb.IsChecked = isMatch;
                    if (isMatch) foundColorSwatch = true;
                }
            }
            if (!foundColorSwatch) {
                double selHue = ShellHelper.GetHueFromHex(_selectedColor);
                foreach (var child in SettingsColorSwatchesPanel.Children) {
                    if (child is RadioButton rb && rb.Tag is string tag) {
                        double swHue = ShellHelper.GetHueFromHex(tag);
                        double diff = Math.Abs(swHue - selHue);
                        if (diff > 180) diff = 360 - diff;
                        if (diff < 4.0) {
                            rb.IsChecked = true;
                            foundColorSwatch = true;
                            break;
                        }
                    }
                }
            }

            SettingsHueSlider.Value = initialHue;

            // Grid columns
            GridColumnsSlider.Value = s.GridColumns;
            GridColumnsValueText.Text = s.GridColumns.ToString();

            // Focus mode
            FocusModeCheckBox.IsChecked = s.FocusModeEnabled;
            FocusDimmingSlider.Value = s.FocusDimmingOpacity * 100;
            FocusDimmingValueText.Text = $"{(int)(s.FocusDimmingOpacity * 100)}%";

            // Lock & Hooks
            LockFoldersCheckBox.IsChecked = s.IsLocked;
            CenterPopupCheckBox.IsChecked = s.OpenInCenterOnly;
            RightDoubleClickCheckBox.IsChecked = s.RightDoubleClickEnabled;
            SpaceQuickLookCheckBox.IsChecked = s.SpaceQuickLookEnabled;
            CombineFilesCheckBox.IsChecked = s.CombineFilesOnDrop;
            AutoStartCheckBox.IsChecked = InstallerService.IsAutoStartEnabled() || s.AutoStart;
            AutoCheckUpdatesCheckBox.IsChecked = s.AutoCheckUpdates;

            // Context Menu status
            bool registered = ContextMenuRegistry.IsRegistered();
            var loc = LocalizationService.Instance;
            ContextMenuStatusText.Text = registered ? loc["ContextMenuInstalled"] : "Not registered in Explorer";
            UpdateUpdateUI();
        }

        public void ApplyLocalization() {
            var loc = LocalizationService.Instance;

            TitleText.Text = loc["Settings"];
            SupportTitleText.Text = loc["Support"];
            SupportDescText.Text = loc["SupportDesc"];
            KofiButton.Content = loc["DonateOnKofi"];
            LanguageLabel.Text = loc["Language"];
            LanguageAiBadge.Text = loc["LanguageAiNote"];
            IconShapeLabel.Text = loc["IconShape"];
            SettingsShapeCircle.ToolTip = loc["Circle"];
            SettingsShapeSquircle.ToolTip = loc["Squircle"];
            SettingsShapeClover.ToolTip = loc["Clover"];
            SettingsShapeScallop.ToolTip = loc["Scallop"];
            SettingsShapeArch.ToolTip = loc["Arch"];
            ThemeSectionLabel.Text = loc["Theme"];
            ThemeSectionDesc.Text = loc["ThemeDesc"];
            SettingsThemeLight.Content = loc["ThemeLight"];
            SettingsThemeDark.Content = loc["ThemeDark"];
            SettingsThemeAuto.Content = loc["ThemeAuto"];
            DefaultColorLabel.Text = loc["DefaultFolderColor"];
            SettingsHueLabel.Text = loc["Hue"];
            GridColumnsLabel.Text = loc["ItemsPerRow"];
            FocusModeLabel.Text = loc["FocusMode"];
            FocusModeDescText.Text = loc["FocusModeDesc"];
            FocusDimmingLabel.Text = loc["FocusDimming"];
            CenterPopupLabel.Text = loc["CenterPopupOnly"];
            CenterPopupDescText.Text = loc["CenterPopupOnlyDesc"];
            LockFoldersLabel.Text = loc["LockFolders"];
            LockFoldersDescText.Text = loc["LockFoldersDesc"];
            RightDoubleClickLabel.Text = loc["RightDoubleClick"];
            RightDoubleClickDescText.Text = loc["RightDoubleClickDesc"];
            SpaceQuickLookLabel.Text = loc["SpaceQuickLook"];
            SpaceQuickLookDescText.Text = loc["SpaceQuickLookDesc"];
            CombineFilesLabel.Text = loc["CombineFilesOnDrop"];
            CombineFilesDescText.Text = loc["CombineFilesOnDropDesc"];
            AutoStartLabel.Text = loc["AutoStart"];
            UpdatesSectionTitle.Text = loc["Updates"];
            CheckUpdatesBtn.Content = loc["CheckForUpdates"];
            AutoCheckUpdatesLabel.Text = loc["AutoCheckUpdates"];
            AutoCheckUpdatesDesc.Text = loc["AutoCheckUpdatesDesc"];
            ReleaseNotesHeader.Text = loc["ReleaseNotes"] + ":";
            DownloadInstallBtn.Content = loc["DownloadAndInstall"];
            SaveBtn.Content = loc["Save"];
            CancelBtn.Content = loc["Cancel"];
            InstallAppBtn.Content = loc["Install"];
            UninstallAppBtn.Content = loc["Uninstall"];
            InstallerSectionTitle.Text = loc["InstallerTitle"];
            InstallerSectionDesc.Text = loc["InstallDesc"];

            bool registered = ContextMenuRegistry.IsRegistered();
            ContextMenuStatusText.Text = registered ? loc["ContextMenuInstalled"] : "Explorer context menu not registered";
            UpdateUpdateUI();
        }

        private void SettingsColorSwatch_Checked(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.Tag is string hex) {
                _selectedColor = hex;
                double hue = ShellHelper.GetHueFromHex(hex);
                ApplyMaterialYouPalette(hue);
                _isInitializing = true;
                try {
                    SettingsHueSlider.Value = hue;
                } finally {
                    _isInitializing = false;
                }
            }
        }

        private void SettingsHueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing) return;
            string pastelHex = ShellHelper.ColorFromPastelHueHex(e.NewValue);
            _selectedColor = pastelHex;
            ApplyMaterialYouPalette(e.NewValue);

            bool matched = false;
            foreach (var child in SettingsColorSwatchesPanel.Children) {
                if (child is RadioButton rb && rb.Tag is string tag) {
                    double swatchHue = ShellHelper.GetHueFromHex(tag);
                    double diff = Math.Abs(swatchHue - e.NewValue);
                    if (diff > 180) diff = 360 - diff;
                    bool isMatch = diff < 4.0;
                    if (rb.IsChecked != isMatch) {
                        rb.IsChecked = isMatch;
                    }
                    if (isMatch) matched = true;
                }
            }
            if (!matched) {
                foreach (var child in SettingsColorSwatchesPanel.Children) {
                    if (child is RadioButton rb && rb.IsChecked == true) {
                        rb.IsChecked = false;
                    }
                }
            }
        }

        private void SettingsShape_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            if (sender == SettingsShapeCircle) _selectedShape = "Circle";
            else if (sender == SettingsShapeSquircle) _selectedShape = "Squircle";
            else if (sender == SettingsShapeClover) _selectedShape = "Clover";
            else if (sender == SettingsShapeScallop) _selectedShape = "Scallop";
            else if (sender == SettingsShapeArch) _selectedShape = "Arch";
        }

        private void SettingsTheme_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            if (sender is RadioButton rb && rb.Tag is string mode) {
                _selectedThemeMode = mode;
                double hue = ShellHelper.GetHueFromHex(_selectedColor);
                ApplyMaterialYouPalette(hue);
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_isInitializing) {
                return;
            }

            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode) {
                LocalizationService.Instance.CurrentLanguage = langCode;
                ApplyLocalization();
            }
        }

        private void GridColumnsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GridColumnsValueText != null) {
                GridColumnsValueText.Text = ((int)e.NewValue).ToString();
            }
        }

        private void FocusDimmingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (FocusDimmingValueText != null) {
                FocusDimmingValueText.Text = $"{(int)e.NewValue}%";
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;

            if (LanguageComboBox.SelectedItem is ComboBoxItem langItem && langItem.Tag is string lang) {
                s.Language = lang;
            }

            s.IconShape = _selectedShape;
            s.ThemeColor = _selectedColor;
            s.ThemeMode = _selectedThemeMode;

            s.GridColumns = (int)GridColumnsSlider.Value;
            s.FocusModeEnabled = FocusModeCheckBox.IsChecked ?? true;
            s.FocusDimmingOpacity = FocusDimmingSlider.Value / 100.0;
            s.IsLocked = LockFoldersCheckBox.IsChecked ?? false;
            s.OpenInCenterOnly = CenterPopupCheckBox.IsChecked ?? false;
            s.RightDoubleClickEnabled = RightDoubleClickCheckBox.IsChecked ?? true;
            s.SpaceQuickLookEnabled = SpaceQuickLookCheckBox.IsChecked ?? true;
            s.CombineFilesOnDrop = CombineFilesCheckBox.IsChecked ?? true;
            s.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            s.AutoCheckUpdates = AutoCheckUpdatesCheckBox.IsChecked ?? true;
            s.GitHubRepo = GitHubUpdateService.TargetRepository;

            // Handle auto-start setting
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            InstallerService.SetAutoStart(exe, s.AutoStart);

            SettingsService.Instance.SaveSettings(s);
            Close();
        }

        private void UpdateUpdateUI() {
            var updater = GitHubUpdateService.Instance;
            var loc = LocalizationService.Instance;

            UpdateStatusTitleText.Text = $"{loc["CurrentVersion"]}: v{updater.CurrentVersionString}";

            switch (updater.Status) {
                case UpdateStatus.Checking:
                    UpdateStatusDetailText.Text = loc["CheckingForUpdates"];
                    UpdateStatusBadge.Text = "⏳";
                    CheckUpdatesBtn.IsEnabled = false;
                    UpdateAvailableCard.Visibility = Visibility.Collapsed;
                    break;
                case UpdateStatus.UpdateAvailable:
                    UpdateStatusDetailText.Text = $"{loc["UpdateAvailable"]}: v{updater.LatestVersionString}";
                    UpdateStatusBadge.Text = "★ " + loc["UpdateAvailable"];
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateAvailableCard.Visibility = Visibility.Visible;
                    NewVersionAvailableText.Text = $"v{updater.LatestVersionString} - {updater.ReleaseTitle ?? "Release"}";
                    NewVersionDateText.Text = updater.PublishedAt.HasValue ? updater.PublishedAt.Value.ToLocalTime().ToString("d MMM yyyy") : "";
                    ReleaseNotesContentText.Text = string.IsNullOrWhiteSpace(updater.ReleaseNotes) ? "(No release notes)" : updater.ReleaseNotes;
                    DownloadInstallBtn.IsEnabled = true;
                    DownloadInstallBtn.Content = loc["DownloadAndInstall"];
                    break;
                case UpdateStatus.Downloading:
                    UpdateStatusDetailText.Text = loc["DownloadingUpdate"];
                    UpdateStatusBadge.Text = $"{updater.DownloadProgress:0}%";
                    CheckUpdatesBtn.IsEnabled = false;
                    UpdateAvailableCard.Visibility = Visibility.Visible;
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.Value = updater.DownloadProgress;
                    DownloadInstallBtn.IsEnabled = false;
                    DownloadInstallBtn.Content = $"{loc["DownloadingUpdate"]} {updater.DownloadProgress:0}%";
                    break;
                case UpdateStatus.ReadyToInstall:
                    UpdateStatusDetailText.Text = "Ready to install";
                    UpdateStatusBadge.Text = "✓ Ready";
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    DownloadInstallBtn.IsEnabled = true;
                    DownloadInstallBtn.Content = loc["Install"];
                    break;
                case UpdateStatus.Error:
                    UpdateStatusDetailText.Text = updater.ErrorMessage ?? loc["UpdateError"];
                    UpdateStatusBadge.Text = "⚠ " + loc["UpdateError"];
                    CheckUpdatesBtn.IsEnabled = true;
                    break;
                case UpdateStatus.UpToDate:
                default:
                    UpdateStatusDetailText.Text = loc["UpToDate"];
                    UpdateStatusBadge.Text = "✓ " + loc["LatestVersion"];
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateAvailableCard.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void OnUpdateStatusChanged(UpdateStatus status, string? msg) {
            Dispatcher.Invoke(UpdateUpdateUI);
        }

        private void OnDownloadProgressChanged(double progress) {
            Dispatcher.Invoke(() => {
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = progress;
                DownloadInstallBtn.Content = $"{LocalizationService.Instance["DownloadingUpdate"]} {progress:0}%";
            });
        }

        private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e) {
            SettingsService.Instance.Current.GitHubRepo = GitHubUpdateService.TargetRepository;
            SettingsService.Instance.Save();

            CheckUpdatesBtn.IsEnabled = false;
            UpdateStatusDetailText.Text = LocalizationService.Instance["CheckingForUpdates"];
            await GitHubUpdateService.Instance.CheckForUpdatesAsync(manual: true);
            UpdateUpdateUI();
        }

        private async void DownloadInstallBtn_Click(object sender, RoutedEventArgs e) {
            DownloadInstallBtn.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;
            await GitHubUpdateService.Instance.DownloadAndApplyUpdateAsync(p => {
                Dispatcher.Invoke(() => UpdateProgressBar.Value = p);
            });
        }

        private void KofiButton_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/hranthakobian/") { UseShellExecute = true });
            } catch { }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void ReRegisterContextBtn_Click(object sender, RoutedEventArgs e) {
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            ContextMenuRegistry.RegisterAll(exe);
            ApplyLocalization();
            MessageBox.Show(LocalizationService.Instance["ContextMenuInstalled"], "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void InstallAppBtn_Click(object sender, RoutedEventArgs e) {
            bool success = InstallerService.Install(createDesktopShortcut: true, createStartMenuShortcut: true, autoStart: AutoStartCheckBox.IsChecked ?? true);
            if (success) {
                MessageBox.Show(LocalizationService.Instance["InstalledSuccess"], "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
            } else {
                MessageBox.Show("Installation encountered an issue. Please try again.", "Panak", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UninstallAppBtn_Click(object sender, RoutedEventArgs e) {
            var result = MessageBox.Show(LocalizationService.Instance["UninstallDesc"] + "\n\nAre you sure you want to proceed?", "Panak", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) {
                bool success = InstallerService.Uninstall();
                if (success) {
                    MessageBox.Show(LocalizationService.Instance["UninstalledSuccess"], "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                } else {
                    MessageBox.Show("Uninstall encountered an issue.", "Panak", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
