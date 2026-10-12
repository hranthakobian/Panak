using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Panak.Models;
using Panak.Services;

namespace Panak.Views {
    public partial class SettingsWindow : Window {
        private bool _isInitializing = true;
        private string _selectedShape = "Squircle";
        private string _selectedColor = ShellHelper.DefaultPastelColorHex;
        private string _selectedThemeMode = "Auto";
        private string _initialColor = ShellHelper.DefaultPastelColorHex;
        private string _initialThemeMode = "Auto";
        private string _currentQuickLookHotkey = "Space";
        private string _currentClosePopupHotkey = "Escape";
        private string? _recordingTarget = null;
        private bool _isAppliedOrSaved = false;

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
            BitmapImage? bi = null;
            try {
                bi = new BitmapImage(new Uri("pack://application:,,,/Logo.png", UriKind.Absolute));
            } catch {
                try {
                    bi = new BitmapImage(new Uri("pack://application:,,,/SettingsLogo.png", UriKind.Absolute));
                } catch { }
            }

            if (bi == null) {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(baseDir, "src", "Logo.png"),
                    Path.Combine(baseDir, "Logo.png"),
                    Path.Combine(baseDir, "src", "SettingsLogo.png"),
                    Path.Combine(baseDir, "SettingsLogo.png"),
                    Path.Combine(baseDir, "PanakLogo.png"),
                    Path.Combine(baseDir, "Panak logo.png"),
                    Path.Combine(baseDir, "Panak.ico")
                };
                foreach (var file in candidates) {
                    if (File.Exists(file)) {
                        bi = new BitmapImage(new Uri(file, UriKind.Absolute));
                        break;
                    }
                }
            }

            if (bi != null) {
                Icon = bi;
            }
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

                // Update dynamic brushes in Resources with frozen brushes so DynamicResource bindings update reliably across all templates
                void SetBrush(string key, System.Windows.Media.Color col) {
                    var brush = new System.Windows.Media.SolidColorBrush(col);
                    brush.Freeze();
                    Resources[key] = brush;
                }

                SetBrush("M3SurfaceBrush", wpfSurface);
                SetBrush("M3SurfaceContainerBrush", wpfContainer);
                SetBrush("M3CardBrush", wpfCard);
                SetBrush("M3PrimaryBrush", wpfPrimary);
                SetBrush("M3OnPrimaryBrush", wpfOnPrimary);
                SetBrush("M3OnSurfaceBrush", wpfOnSurface);
                SetBrush("M3OnSurfaceVariantBrush", wpfOnSurfaceVariant);
                SetBrush("M3TrackInactiveBrush", wpfTrackInactive);

                var wpfErrorContainer = isDark
                    ? toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.45, 0.25))
                    : toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.65, 0.88));
                var wpfOnErrorContainer = isDark
                    ? toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.80, 0.90))
                    : toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.80, 0.22));
                SetBrush("M3ErrorContainerBrush", wpfErrorContainer);
                SetBrush("M3OnErrorContainerBrush", wpfOnErrorContainer);

                // Dynamically update the 5 vector logo colors based on active theme hue & luminance
                var (cContainer, cTriangle, cCloud, cShield, cEllipse) = IconGenerator.GetLogoDynamicColors(hue, isDark);
                SetBrush("LogoContainerBrush", toWpf(cContainer));
                SetBrush("LogoTriangleBrush", toWpf(cTriangle));
                SetBrush("LogoCloudBrush", toWpf(cCloud));
                SetBrush("LogoShieldBrush", toWpf(cShield));
                SetBrush("LogoEllipseBrush", toWpf(cEllipse));

                // Update Tray Icon in real time with vector logo theme
                TrayManager.Instance?.UpdateTrayIconTheme(hue, isDark);

                // Update Window Icon dynamically
                try {
                    using var bmp = IconGenerator.RenderPanakLogoBitmap(32, hue, isDark);
                    IntPtr hIcon = bmp.GetHicon();
                    Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    ShellHelper.DestroyIcon(hIcon);
                } catch { }

                System.Windows.Media.Color selColor;
                try {
                    selColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_selectedColor);
                } catch {
                    selColor = wpfPrimary;
                }
                SetBrush("SettingsHueThumbBrush", selColor);

                if (MainWindowBorder != null) {
                    MainWindowBorder.Background = new System.Windows.Media.SolidColorBrush(wpfSurface);
                }
                if (SettingsColorIndicator != null) {
                    SettingsColorIndicator.Background = new System.Windows.Media.SolidColorBrush(selColor);
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
            _initialColor = _selectedColor;
            _initialThemeMode = _selectedThemeMode;
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

            // General folder opacity
            GeneralFolderOpacitySlider.Value = Math.Round(s.FolderOpacity * 100);
            GeneralFolderOpacityValueText.Text = $"{(int)GeneralFolderOpacitySlider.Value}%";

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

            _currentQuickLookHotkey = s.QuickLookHotkey ?? "Space";
            _currentClosePopupHotkey = s.ClosePopupHotkey ?? "Escape";
            UpdateKeyBadgesUI();

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
            GeneralFolderOpacityLabel.Text = loc["GeneralFolderOpacity"];
            GeneralFolderOpacityDescText.Text = loc["GeneralFolderOpacityDesc"];
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
            if (KeyRebindingLabel != null) KeyRebindingLabel.Text = loc["KeyRebinding"];
            if (KeyRebindingDescText != null) KeyRebindingDescText.Text = loc["KeyRebindingDesc"];
            if (ResetKeysBtn != null) ResetKeysBtn.Content = loc["ResetKeys"];
            if (QuickLookKeyRowLabel != null) QuickLookKeyRowLabel.Text = loc["QuickLookKeyRow"];
            if (QuickLookKeyRowDesc != null) QuickLookKeyRowDesc.Text = loc["QuickLookKeyRowDesc"];
            if (ClosePopupKeyRowLabel != null) ClosePopupKeyRowLabel.Text = loc["ClosePopupKeyRow"];
            if (ClosePopupKeyRowDesc != null) ClosePopupKeyRowDesc.Text = loc["ClosePopupKeyRowDesc"];
            UpdateKeyBadgesUI();
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
            ApplyBtn.Content = loc["Apply"];
            CancelBtn.Content = loc["Cancel"];
            UninstallAppBtn.Content = loc["Uninstall"];
            InstallerSectionTitle.Text = loc["Uninstaller"];

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

                if (PanakManager.Instance.IsPopupOpen) {
                    PanakManager.Instance.UpdateOpenPopupThemeColor(_selectedColor);
                }
            }
        }

        private void SettingsHueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing) return;
            string pastelHex = ShellHelper.ColorFromPastelHueHex(e.NewValue);
            _selectedColor = pastelHex;
            ApplyMaterialYouPalette(e.NewValue);

            if (PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupThemeColor(_selectedColor);
            }

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

                if (PanakManager.Instance.IsPopupOpen) {
                    PanakManager.Instance.UpdateOpenPopupThemeMode(_selectedThemeMode);
                }
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_isInitializing) {
                return;
            }

            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode) {
                LocalizationService.Instance.CurrentLanguage = langCode;
                ApplyLocalization();

                try {
                    SettingsService.Instance.Current.Language = langCode;
                    SettingsService.Instance.SaveSettings();

                    string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrEmpty(exe) && ContextMenuRegistry.IsRegistered()) {
                        ContextMenuRegistry.RegisterAll(exe, langCode);
                    }
                } catch { }
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

        private void GeneralFolderOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GeneralFolderOpacityValueText != null) {
                GeneralFolderOpacityValueText.Text = $"{(int)e.NewValue}%";
            }
            if (!_isInitializing && PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupOpacity(e.NewValue / 100.0);
            }
        }

        public void ApplyCurrentSettings() {
            var s = SettingsService.Instance.Current;

            if (LanguageComboBox.SelectedItem is ComboBoxItem langItem && langItem.Tag is string lang) {
                s.Language = lang;
            }

            s.IconShape = _selectedShape;
            s.ThemeColor = _selectedColor;
            s.ThemeMode = _selectedThemeMode;
            s.FolderOpacity = GeneralFolderOpacitySlider.Value / 100.0;

            s.GridColumns = (int)GridColumnsSlider.Value;
            s.FocusModeEnabled = FocusModeCheckBox.IsChecked ?? true;
            s.FocusDimmingOpacity = FocusDimmingSlider.Value / 100.0;
            s.IsLocked = LockFoldersCheckBox.IsChecked ?? false;
            s.OpenInCenterOnly = CenterPopupCheckBox.IsChecked ?? false;
            s.RightDoubleClickEnabled = RightDoubleClickCheckBox.IsChecked ?? true;
            s.SpaceQuickLookEnabled = SpaceQuickLookCheckBox.IsChecked ?? true;
            s.QuickLookHotkey = _currentQuickLookHotkey;
            s.ClosePopupHotkey = _currentClosePopupHotkey;
            s.CombineFilesOnDrop = CombineFilesCheckBox.IsChecked ?? true;
            s.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            s.AutoCheckUpdates = AutoCheckUpdatesCheckBox.IsChecked ?? true;
            s.GitHubRepo = GitHubUpdateService.TargetRepository;

            // Handle auto-start setting
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            InstallerService.SetAutoStart(exe, s.AutoStart);

            SettingsService.Instance.SaveSettings(s);
            PanakManager.Instance.UpdateOpenPopupThemeColor(s.ThemeColor);
            PanakManager.Instance.UpdateOpenPopupThemeMode(s.ThemeMode);
            PanakManager.Instance.UpdateOpenPopupOpacity(s.FolderOpacity);

            try {
                if (!string.IsNullOrEmpty(exe) && ContextMenuRegistry.IsRegistered()) {
                    ContextMenuRegistry.RegisterAll(exe, s.Language);
                }
            } catch { }

            _initialColor = _selectedColor;
            _initialThemeMode = _selectedThemeMode;
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e) {
            ApplyCurrentSettings();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e) {
            ApplyCurrentSettings();
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
                    UpdateStatusDetailText.Text = loc["ReadyToInstall"] ?? "Ready to install";
                    UpdateStatusBadge.Text = "✓ " + (loc["ReadyToInstall"] ?? "Ready");
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateAvailableCard.Visibility = Visibility.Visible;
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.Value = 100;
                    DownloadInstallBtn.IsEnabled = true;
                    DownloadInstallBtn.Content = loc["InstallNow"] ?? loc["Install"] ?? "Install Now";
                    break;
                case UpdateStatus.Error:
                    UpdateStatusDetailText.Text = updater.ErrorMessage ?? loc["UpdateError"];
                    UpdateStatusBadge.Text = "⚠ " + loc["UpdateError"];
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    DownloadInstallBtn.IsEnabled = true;
                    DownloadInstallBtn.Content = loc["DownloadAndInstall"];
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
            var updater = GitHubUpdateService.Instance;

            // If update is already downloaded and ready to install, DO NOT re-download! Apply it immediately!
            if (updater.Status == UpdateStatus.ReadyToInstall && !string.IsNullOrEmpty(updater.DownloadedFilePath) && System.IO.File.Exists(updater.DownloadedFilePath)) {
                DownloadInstallBtn.IsEnabled = false;
                DownloadInstallBtn.Content = LocalizationService.Instance["Installing"] ?? "Installing...";
                bool launched = updater.ApplyDownloadedUpdate();
                if (!launched) {
                    DownloadInstallBtn.IsEnabled = true;
                    UpdateUpdateUI();
                }
                return;
            }

            // Otherwise, start download
            DownloadInstallBtn.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;

            bool downloadSuccess = await updater.DownloadUpdateAsync(p => {
                Dispatcher.Invoke(() => UpdateProgressBar.Value = p);
            });

            if (downloadSuccess) {
                // Immediately apply downloaded update
                bool launched = updater.ApplyDownloadedUpdate();
                if (!launched) {
                    DownloadInstallBtn.IsEnabled = true;
                    UpdateUpdateUI();
                }
            } else {
                DownloadInstallBtn.IsEnabled = true;
                UpdateUpdateUI();
            }
        }

        private void KofiButton_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/hranthakobian/") { UseShellExecute = true });
            } catch { }
        }

        private void EmailLink_MouseDown(object sender, MouseButtonEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("mailto:dev@hakobian.am") { UseShellExecute = true });
            } catch { }
        }

        private void WebsiteLink_MouseDown(object sender, MouseButtonEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://hakobian.am/contact") { UseShellExecute = true });
            } catch { }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) {
            if (PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupThemeColor(_initialColor);
                PanakManager.Instance.UpdateOpenPopupThemeMode(_initialThemeMode);
            }
            try {
                double initialHue = ShellHelper.GetHueFromHex(_initialColor);
                bool initialDark = _initialThemeMode == "Dark" || (_initialThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                TrayManager.Instance?.UpdateTrayIconTheme(initialHue, initialDark);
            } catch { }
            _isAppliedOrSaved = true;
            Close();
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            if (!_isAppliedOrSaved) {
                try {
                    double initialHue = ShellHelper.GetHueFromHex(_initialColor);
                    bool initialDark = _initialThemeMode == "Dark" || (_initialThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                    TrayManager.Instance?.UpdateTrayIconTheme(initialHue, initialDark);
                } catch { }
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void ReRegisterContextBtn_Click(object sender, RoutedEventArgs e) {
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            ContextMenuRegistry.RegisterAll(exe, LocalizationService.Instance.CurrentLanguage);
            ApplyLocalization();
            MessageBox.Show(LocalizationService.Instance["ContextMenuInstalled"], "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UninstallAppBtn_Click(object sender, RoutedEventArgs e) {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localAppDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "Panak");

                string[] candidates = {
                    Path.Combine(baseDir, "Uninstall.exe"),
                    Path.Combine(localAppDir, "Uninstall.exe"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "Uninstall", "bin", "Debug", "net8.0-windows10.0.19041.0", "Uninstall.exe"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "publish", "Uninstall.exe"),
                    Path.Combine(baseDir, "Setup.exe"),
                    Path.Combine(localAppDir, "Setup.exe")
                };

                foreach (var candidate in candidates) {
                    if (File.Exists(candidate)) {
                        Process.Start(new ProcessStartInfo {
                            FileName = Path.GetFullPath(candidate),
                            Arguments = candidate.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase) ? "--uninstall" : "",
                            UseShellExecute = true
                        });
                        Close();
                        return;
                    }
                }

                new InstallerWindow(isUninstallMode: true).Show();
                Close();
            } catch (Exception ex) {
                MessageBox.Show($"Could not launch uninstaller: {ex.Message}", "Panak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenderKeyBadges(StackPanel container, string hotkey, string target) {
            container.Children.Clear();

            bool isRecording = (_recordingTarget == target);
            if (isRecording) {
                var recBtn = new Button {
                    Style = (Style)FindResource("KeycapBadgeButtonStyle"),
                    Content = LocalizationService.Instance["PressAnyKey"],
                    Tag = target,
                    BorderThickness = new Thickness(2),
                    BorderBrush = (Brush)FindResource("M3PrimaryBrush"),
                    Background = (Brush)FindResource("M3SurfaceContainerBrush")
                };
                recBtn.Click += KeyBadgeButton_Click;
                container.Children.Add(recBtn);
                return;
            }

            var parts = hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) parts = new[] { "None" };

            for (int i = 0; i < parts.Length; i++) {
                if (i > 0) {
                    var plusText = new TextBlock {
                        Text = "+",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)FindResource("M3OnSurfaceVariantBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 4, 0)
                    };
                    container.Children.Add(plusText);
                }

                string keyName = parts[i];
                if (keyName.Equals("Escape", StringComparison.OrdinalIgnoreCase)) keyName = "Esc";
                else if (keyName.Equals("Return", StringComparison.OrdinalIgnoreCase)) keyName = "Enter";

                var btn = new Button {
                    Style = (Style)FindResource("KeycapBadgeButtonStyle"),
                    Content = keyName,
                    Tag = target
                };
                btn.Click += KeyBadgeButton_Click;
                container.Children.Add(btn);
            }
        }

        private void KeyBadgeButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.Tag is string target) {
                if (_recordingTarget == target) {
                    _recordingTarget = null;
                } else {
                    _recordingTarget = target;
                }
                UpdateKeyBadgesUI();
            }
        }

        private void ResetKeysBtn_Click(object sender, RoutedEventArgs e) {
            _recordingTarget = null;
            _currentQuickLookHotkey = "Space";
            _currentClosePopupHotkey = "Escape";
            UpdateKeyBadgesUI();
        }

        private void UpdateKeyBadgesUI() {
            if (QuickLookKeysContainer != null) {
                RenderKeyBadges(QuickLookKeysContainer, _currentQuickLookHotkey, "QuickLook");
            }
            if (ClosePopupKeysContainer != null) {
                RenderKeyBadges(ClosePopupKeysContainer, _currentClosePopupHotkey, "ClosePopup");
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (_recordingTarget == null) {
                if (e.Key == Key.Escape) {
                    CancelBtn_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                return;
            }

            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin) {
                return;
            }

            var parts = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");

            string keyStr = key.ToString();
            if (key == Key.Escape) keyStr = "Escape";
            else if (key == Key.Space) keyStr = "Space";

            parts.Add(keyStr);
            string finalHotkey = string.Join(" + ", parts);

            if (_recordingTarget == "QuickLook") {
                _currentQuickLookHotkey = finalHotkey;
            } else if (_recordingTarget == "ClosePopup") {
                _currentClosePopupHotkey = finalHotkey;
            }

            _recordingTarget = null;
            UpdateKeyBadgesUI();
            e.Handled = true;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (_recordingTarget != null) {
                var source = e.OriginalSource as DependencyObject;
                bool isInside = false;
                while (source != null) {
                    if (source is Button b && (string)b.Tag == _recordingTarget) {
                        isInside = true;
                        break;
                    }
                    source = VisualTreeHelper.GetParent(source);
                }
                if (!isInside) {
                    _recordingTarget = null;
                    UpdateKeyBadgesUI();
                }
            }
        }
    }
}
