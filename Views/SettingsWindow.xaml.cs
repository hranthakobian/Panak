using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private RadioButton? SettingsAccentSwatch = null;
        private static readonly (string Hex, string Name)[] DefaultPresetColors = new[] {
            (ShellHelper.DefaultPastelColorHex, "Peach / Default"),
            ("#C5FCC5", "Mint"),
            ("#C5FCFC", "Cyan"),
            ("#C5DDFC", "Sky Blue"),
            ("#DDC5FC", "Lavender"),
            ("#FCC5FC", "Lilac"),
            ("#FCC5DD", "Rose"),
            ("#FCFCC5", "Butter Yellow")
        };

        public SettingsWindow() {
            InitializeComponent();
            LoadLogoAndIcon();
            LoadCurrentSettings();
            ApplyLocalization();
            StartWallpaperCapture();
            UpdateLivePreview();
            CheckSettingsLockState();
            _isInitializing = false;

            Loaded += (s, e) => UpdateWallpaperCrop();
            LocationChanged += (s, e) => UpdateWallpaperCrop();

            FocusModeCheckBox.Click += (s, e) => { UpdateFocusModeControlsEnabled(animate: true); UpdateLivePreview(); };
            FocusBlurCheckBox.Click += (s, e) => { UpdateFocusModeControlsEnabled(animate: true); UpdateLivePreview(); };
            FolderBlurCheckBox.Click += (s, e) => { UpdateFolderBlurControlsEnabled(animate: true); UpdateLivePreview(); };

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
            RebuildPresetSwatchesUI();
            RebuildCustomSwatchesUI();

            bool foundColorSwatch = false;
            foreach (var rb in GetAllColorSwatchRadioButtons()) {
                if (rb.Tag is string tag) {
                    bool isMatch = string.Equals(tag, _selectedColor, StringComparison.OrdinalIgnoreCase);
                    rb.IsChecked = isMatch;
                    if (isMatch) foundColorSwatch = true;
                }
            }
            if (!foundColorSwatch) {
                double selHue = ShellHelper.GetHueFromHex(_selectedColor);
                foreach (var rb in GetAllColorSwatchRadioButtons()) {
                    if (rb.Tag is string tag) {
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

            // Folder blur
            FolderBlurCheckBox.IsChecked = s.FolderBlurEnabled;
            FolderBlurSlider.Value = s.FolderBlurRadius;
            FolderBlurValueText.Text = $"{(int)s.FolderBlurRadius} px";
            UpdateFolderBlurControlsEnabled(animate: false);

            // Paging mode & rows
            if (string.Equals(s.PagingMode, "Scrollbar", StringComparison.OrdinalIgnoreCase)) {
                PagingScrollbarRadio.IsChecked = true;
            } else {
                PagingSliderRadio.IsChecked = true;
            }
            GridRowsSlider.Value = s.GridRows;
            GridRowsValueText.Text = s.GridRows.ToString();
            SliderArrowsCheckBox.IsChecked = s.SliderArrowsEnabled;
            SliderDragCheckBox.IsChecked = s.SliderDragEnabled;
            UpdatePagingControlsVisibility();

            // Grid columns
            GridColumnsSlider.Value = s.GridColumns;
            GridColumnsValueText.Text = s.GridColumns.ToString();

            // Focus mode
            FocusModeCheckBox.IsChecked = s.FocusModeEnabled;
            FocusDimmingSlider.Value = s.FocusDimmingOpacity * 100;
            FocusDimmingValueText.Text = $"{(int)(s.FocusDimmingOpacity * 100)}%";
            FocusBlurCheckBox.IsChecked = s.FocusBlurEnabled;
            FocusBlurSlider.Value = s.FocusBlurRadius;
            FocusBlurValueText.Text = $"{(int)s.FocusBlurRadius} px";
            UpdateFocusModeControlsEnabled(animate: false);

            // Lock & Hooks
            LockFoldersCheckBox.IsChecked = s.IsLocked;
            if (LockPopupPositionCheckBox != null) LockPopupPositionCheckBox.IsChecked = s.LockPopupPosition;
            CenterPopupCheckBox.IsChecked = s.OpenInCenterOnly;
            RightDoubleClickCheckBox.IsChecked = s.RightDoubleClickEnabled;
            SpaceQuickLookCheckBox.IsChecked = s.SpaceQuickLookEnabled;
            CombineFilesCheckBox.IsChecked = s.CombineFilesOnDrop;
            AutoStartCheckBox.IsChecked = InstallerService.IsAutoStartEnabled() || s.AutoStart;
            AutoCheckUpdatesCheckBox.IsChecked = s.AutoCheckUpdates;

            if (RandomShapeCheckBox != null) RandomShapeCheckBox.IsChecked = s.RandomShapeOnCreate;
            if (RandomColorCheckBox != null) RandomColorCheckBox.IsChecked = s.RandomColorOnCreate;
            if (RandomOpacityCheckBox != null) RandomOpacityCheckBox.IsChecked = s.RandomOpacityOnCreate;

            RebuildCustomSwatchesUI();

            // Context Menu status
            bool registered = ContextMenuRegistry.IsRegistered();
            var loc = LocalizationService.Instance;
            ContextMenuStatusText.Text = registered ? loc["ContextMenuInstalled"] : "Not registered in Explorer";

            _currentQuickLookHotkey = s.QuickLookHotkey ?? "Space";
            _currentClosePopupHotkey = s.ClosePopupHotkey ?? "Escape";
            UpdateKeyBadgesUI();

            UpdateUpdateUI();
            UpdateSettingsLockUI();
        }

        public void ApplyLocalization() {
            var loc = LocalizationService.Instance;

            TitleText.Text = loc["Settings"];
            SupportTitleText.Text = loc["Support"];
            SupportDescText.Text = loc["SupportDesc"];
            KofiButton.Content = loc["DonateOnKofi"];
            if (CrowdinButton != null) CrowdinButton.ToolTip = loc["TranslateOnCrowdin"];
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
            FolderBlurLabel.Text = loc["FolderBlur"];
            if (FolderBlurWarningBadge != null) FolderBlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            FolderBlurDescText.Text = loc["FolderBlurDesc"];
            FolderBlurRadiusLabel.Text = loc["FolderBlurRadius"];
            PagingModeLabel.Text = loc["PagingMode"];
            PagingModeDescText.Text = loc["PagingModeDesc"];
            PagingSliderRadio.Content = loc["PagingModeSlider"];
            PagingScrollbarRadio.Content = loc["PagingModeScrollbar"];
            GridRowsLabel.Text = loc["GridRows"];
            SliderArrowsLabel.Text = loc["SliderArrows"];
            SliderDragLabel.Text = loc["SliderDrag"];
            GridColumnsLabel.Text = loc["ItemsPerRow"];
            FocusModeLabel.Text = loc["FocusMode"];
            FocusModeDescText.Text = loc["FocusModeDesc"];
            FocusDimmingLabel.Text = loc["FocusDimming"];
            FocusBlurLabel.Text = loc["FocusBlur"];
            if (FocusBlurWarningBadge != null) FocusBlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            FocusBlurDescText.Text = loc["FocusBlurDesc"];
            FocusBlurRadiusLabel.Text = loc["FocusBlurRadius"];
            CenterPopupLabel.Text = loc["CenterPopupOnly"];
            CenterPopupDescText.Text = loc["CenterPopupOnlyDesc"];
            LockFoldersLabel.Text = loc["LockFolders"];
            LockFoldersDescText.Text = loc["LockFoldersDesc"];
            if (LockPopupPositionLabel != null) LockPopupPositionLabel.Text = loc["LockPopupPosition"];
            if (LockPopupPositionDescText != null) LockPopupPositionDescText.Text = loc["LockPopupPositionDesc"];
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

            if (RandomShapeCheckBox != null) RandomShapeCheckBox.ToolTip = loc["RandomShape"] ?? "Random shape";
            if (RandomColorCheckBox != null) RandomColorCheckBox.ToolTip = loc["RandomColor"] ?? "Random color";
            if (RandomOpacityCheckBox != null) RandomOpacityCheckBox.ToolTip = loc["RandomOpacity"] ?? "Random opacity";
            if (RandomShapeLabel != null) RandomShapeLabel.Text = loc["RandomByChance"] ?? "Պատահականությամբ";
            if (RandomColorLabel != null) RandomColorLabel.Text = loc["RandomByChance"] ?? "Պատահականությամբ";
            if (RandomOpacityLabel != null) RandomOpacityLabel.Text = loc["RandomByChance"] ?? "Պատահականությամբ";
            if (RestorePresetsMenuItem != null) RestorePresetsMenuItem.Header = loc["RestoreDefaultColors"] ?? "Վերականգնել լռելյայն գույները";
            if (ExportSettingsBtn != null) ExportSettingsBtn.Content = loc["ExportSettings"] ?? "Export";
            if (ImportSettingsBtn != null) ImportSettingsBtn.Content = loc["ImportSettings"] ?? "Import";
            if (ExportImportSectionTitle != null) ExportImportSectionTitle.Text = loc["BackupAndRestore"] ?? "Backup & Restore";
            if (ExportImportSectionDesc != null) ExportImportSectionDesc.Text = loc["ExportImportDesc"] ?? "Export or import configuration via JSON file";

            bool registered = ContextMenuRegistry.IsRegistered();
            ContextMenuStatusText.Text = registered ? loc["ContextMenuInstalled"] : "Explorer context menu not registered";
            if (NavTabAppearance != null) NavTabAppearance.Content = loc["AppearanceTab"] ?? "🎨 Արտաքին";
            if (NavTabLayout != null) NavTabLayout.Content = loc["LayoutTab"] ?? "📐 Դիրք";
            if (NavTabSecurity != null) NavTabSecurity.Content = loc["SecurityTab"] ?? "🔒 Անվտանգություն";
            if (NavTabSystem != null) NavTabSystem.Content = loc["SystemTab"] ?? "⚙️ Համակարգ";
            if (LockSettingsLabel != null) LockSettingsLabel.Text = loc["LockSettings"] ?? "Կողպել Կայանքները";
            if (LockSettingsDescText != null) LockSettingsDescText.Text = loc["LockSettingsDesc"] ?? "Պահանջել գաղտնաբառ/PIN/նախշ/կենսաչափություն կայանքները բացելու կամ փոփոխելու համար";
            if (ConfigureSettingsLockBtn != null) ConfigureSettingsLockBtn.Content = loc["ConfigureLock"] ?? "Կարգավորել կողպումը";
            if (TogglePreviewBtnText != null) TogglePreviewBtnText.Text = (_isPreviewVisible ? (loc["HidePreview"] ?? "Նախադիտում") : (loc["ShowPreview"] ?? "Նախադիտում"));
            if (PreviewLiveBadgeText != null) PreviewLiveBadgeText.Text = loc["LivePreview"] ?? "Live Preview";
            if (PreviewEditNameLabel != null) PreviewEditNameLabel.Text = loc["FolderTitle"] ?? "Անուն";
            if (PreviewColorThemeLabel != null) PreviewColorThemeLabel.Text = loc["ColorTheme"] ?? "Գույն և ձև";
            if (PreviewFolderLockHeaderLabel != null) PreviewFolderLockHeaderLabel.Text = loc["LockFolder"] ?? "Կողպել պանակը";
            if (PreviewFolderLockBtnText != null) PreviewFolderLockBtnText.Text = loc["ConfigureLock"] ?? "Կարգավորել կողպումը";
            if (PreviewAlwaysPanakCheckBox != null) PreviewAlwaysPanakCheckBox.Content = loc["AlwaysOpenWithPanak"] ?? "Միշտ բացել որպես Panak";
            if (PreviewUnpackFolderBtnText != null) PreviewUnpackFolderBtnText.Text = loc["UnpackPanakFolder"] ?? "Ապահավաքել Panak-ը";
            UpdateUpdateUI();
            PopulatePreviewItems();
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
                UpdateLivePreview();
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
            UpdateLivePreview();

            bool matched = false;
            foreach (var rb in GetAllColorSwatchRadioButtons()) {
                if (rb.Tag is string tag) {
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
                foreach (var rb in GetAllColorSwatchRadioButtons()) {
                    if (rb.IsChecked == true) {
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
            UpdateLivePreview();
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
                UpdateLivePreview();
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
            UpdateLivePreview();
        }

        private void FocusDimmingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (FocusDimmingValueText != null) {
                FocusDimmingValueText.Text = $"{(int)e.NewValue}%";
            }
        }

        private void FocusBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (FocusBlurValueText != null) {
                FocusBlurValueText.Text = $"{(int)e.NewValue} px";
            }
        }

        private void UpdateFocusModeControlsEnabled(bool animate = false) {
            bool focusOn = FocusModeCheckBox?.IsChecked ?? true;
            bool blurOn = FocusBlurCheckBox?.IsChecked ?? true;

            if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = focusOn;
            if (FocusBlurCheckBox != null) FocusBlurCheckBox.IsEnabled = focusOn;

            SetFocusDimmingSliderVisible(focusOn, animate);
            SetFocusBlurSliderVisible(focusOn && blurOn, animate);
        }

        private void SetFocusDimmingSliderVisible(bool visible, bool animate) {
            if (FocusDimmingContainer == null) return;

            if (!animate) {
                FocusDimmingContainer.BeginAnimation(UIElement.OpacityProperty, null);
                FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

                if (visible) {
                    FocusDimmingContainer.Visibility = Visibility.Visible;
                    FocusDimmingContainer.Opacity = 1.0;
                    FocusDimmingContainer.MaxHeight = double.PositiveInfinity;
                    if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = true;
                } else {
                    FocusDimmingContainer.Visibility = Visibility.Collapsed;
                    FocusDimmingContainer.Opacity = 0.0;
                    FocusDimmingContainer.MaxHeight = 0;
                    if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = false;
                }
                return;
            }

            var ease = new System.Windows.Media.Animation.CubicEase {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
            };

            if (visible) {
                FocusDimmingContainer.Visibility = Visibility.Visible;
                if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = true;

                FocusDimmingContainer.Measure(new Size(
                    FocusDimmingContainer.ActualWidth > 0 ? FocusDimmingContainer.ActualWidth : 350,
                    double.PositiveInfinity));
                double targetHeight = FocusDimmingContainer.DesiredSize.Height > 0
                    ? FocusDimmingContainer.DesiredSize.Height
                    : 65.0;

                double fromHeight = FocusDimmingContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    fromHeight = 0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusDimmingContainer.Opacity,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                    FocusDimmingContainer.MaxHeight = double.PositiveInfinity;
                };

                FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusDimmingContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            } else {
                if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = false;

                double fromHeight = FocusDimmingContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    FocusDimmingContainer.Measure(new Size(
                        FocusDimmingContainer.ActualWidth > 0 ? FocusDimmingContainer.ActualWidth : 350,
                        double.PositiveInfinity));
                    fromHeight = FocusDimmingContainer.DesiredSize.Height > 0
                        ? FocusDimmingContainer.DesiredSize.Height
                        : 65.0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusDimmingContainer.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    bool focusOn = FocusModeCheckBox?.IsChecked ?? true;
                    if (!focusOn) {
                        FocusDimmingContainer.Visibility = Visibility.Collapsed;
                        FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        FocusDimmingContainer.MaxHeight = 0;
                    }
                };

                FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusDimmingContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void SetFocusBlurSliderVisible(bool visible, bool animate) {
            if (FocusBlurSliderContainer == null) return;

            if (!animate) {
                FocusBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, null);
                FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

                if (visible) {
                    FocusBlurSliderContainer.Visibility = Visibility.Visible;
                    FocusBlurSliderContainer.Opacity = 1.0;
                    FocusBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                    if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = true;
                } else {
                    FocusBlurSliderContainer.Visibility = Visibility.Collapsed;
                    FocusBlurSliderContainer.Opacity = 0.0;
                    FocusBlurSliderContainer.MaxHeight = 0;
                    if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = false;
                }
                return;
            }

            var ease = new System.Windows.Media.Animation.CubicEase {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
            };

            if (visible) {
                FocusBlurSliderContainer.Visibility = Visibility.Visible;
                if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = true;

                FocusBlurSliderContainer.Measure(new Size(
                    FocusBlurSliderContainer.ActualWidth > 0 ? FocusBlurSliderContainer.ActualWidth : 350,
                    double.PositiveInfinity));
                double targetHeight = FocusBlurSliderContainer.DesiredSize.Height > 0
                    ? FocusBlurSliderContainer.DesiredSize.Height
                    : 65.0;

                double fromHeight = FocusBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    fromHeight = 0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusBlurSliderContainer.Opacity,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                    FocusBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                };

                FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            } else {
                if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = false;

                double fromHeight = FocusBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    FocusBlurSliderContainer.Measure(new Size(
                        FocusBlurSliderContainer.ActualWidth > 0 ? FocusBlurSliderContainer.ActualWidth : 350,
                        double.PositiveInfinity));
                    fromHeight = FocusBlurSliderContainer.DesiredSize.Height > 0
                        ? FocusBlurSliderContainer.DesiredSize.Height
                        : 65.0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusBlurSliderContainer.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    bool focusOn = FocusModeCheckBox?.IsChecked ?? true;
                    bool blurOn = FocusBlurCheckBox?.IsChecked ?? true;
                    if (!focusOn || !blurOn) {
                        FocusBlurSliderContainer.Visibility = Visibility.Collapsed;
                        FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        FocusBlurSliderContainer.MaxHeight = 0;
                    }
                };

                FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void FolderBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (FolderBlurValueText != null) {
                FolderBlurValueText.Text = $"{(int)e.NewValue} px";
            }
            UpdateLivePreview();
        }

        private void GridRowsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GridRowsValueText != null) {
                GridRowsValueText.Text = ((int)e.NewValue).ToString();
            }
            UpdateLivePreview();
        }

        private void PagingModeRadio_Checked(object sender, RoutedEventArgs e) {
            UpdatePagingControlsVisibility();
            UpdateLivePreview();
        }

        private void UpdatePagingControlsVisibility() {
            bool isSlider = PagingSliderRadio?.IsChecked == true;
            if (GridRowsContainer != null) GridRowsContainer.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
            if (SliderInteractionsContainer != null) SliderInteractionsContainer.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateFolderBlurControlsEnabled(bool animate = false) {
            bool blurOn = FolderBlurCheckBox?.IsChecked ?? true;
            SetFolderBlurSliderVisible(blurOn, animate);
        }

        private void SetFolderBlurSliderVisible(bool visible, bool animate) {
            if (FolderBlurSliderContainer == null) return;

            if (!animate) {
                FolderBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, null);
                FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

                if (visible) {
                    FolderBlurSliderContainer.Visibility = Visibility.Visible;
                    FolderBlurSliderContainer.Opacity = 1.0;
                    FolderBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                    if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = true;
                } else {
                    FolderBlurSliderContainer.Visibility = Visibility.Collapsed;
                    FolderBlurSliderContainer.Opacity = 0.0;
                    FolderBlurSliderContainer.MaxHeight = 0;
                    if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = false;
                }
                return;
            }

            var ease = new System.Windows.Media.Animation.CubicEase {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
            };

            if (visible) {
                FolderBlurSliderContainer.Visibility = Visibility.Visible;
                if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = true;

                FolderBlurSliderContainer.Measure(new Size(
                    FolderBlurSliderContainer.ActualWidth > 0 ? FolderBlurSliderContainer.ActualWidth : 350,
                    double.PositiveInfinity));
                double targetHeight = FolderBlurSliderContainer.DesiredSize.Height > 0
                    ? FolderBlurSliderContainer.DesiredSize.Height
                    : 65.0;

                double fromHeight = FolderBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    fromHeight = 0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FolderBlurSliderContainer.Opacity,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                    FolderBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                };

                FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FolderBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            } else {
                if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = false;

                double fromHeight = FolderBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    FolderBlurSliderContainer.Measure(new Size(
                        FolderBlurSliderContainer.ActualWidth > 0 ? FolderBlurSliderContainer.ActualWidth : 350,
                        double.PositiveInfinity));
                    fromHeight = FolderBlurSliderContainer.DesiredSize.Height > 0
                        ? FolderBlurSliderContainer.DesiredSize.Height
                        : 65.0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FolderBlurSliderContainer.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    bool blurOn = FolderBlurCheckBox?.IsChecked ?? true;
                    if (!blurOn) {
                        FolderBlurSliderContainer.Visibility = Visibility.Collapsed;
                        FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        FolderBlurSliderContainer.MaxHeight = 0;
                    }
                };

                FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FolderBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void GeneralFolderOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GeneralFolderOpacityValueText != null) {
                GeneralFolderOpacityValueText.Text = $"{(int)e.NewValue}%";
            }
            if (!_isInitializing && PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupOpacity(e.NewValue / 100.0);
            }
            UpdateLivePreview();
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
            s.GridRows = (int)GridRowsSlider.Value;
            s.FolderBlurEnabled = FolderBlurCheckBox.IsChecked ?? true;
            s.FolderBlurRadius = FolderBlurSlider.Value;
            s.PagingMode = (PagingScrollbarRadio.IsChecked == true) ? "Scrollbar" : "Slider";
            s.SliderArrowsEnabled = SliderArrowsCheckBox.IsChecked ?? true;
            s.SliderDragEnabled = SliderDragCheckBox.IsChecked ?? true;
            s.FocusModeEnabled = FocusModeCheckBox.IsChecked ?? true;
            s.FocusDimmingOpacity = FocusDimmingSlider.Value / 100.0;
            s.FocusBlurEnabled = FocusBlurCheckBox.IsChecked ?? true;
            s.FocusBlurRadius = FocusBlurSlider.Value;
            s.IsLocked = LockFoldersCheckBox.IsChecked ?? false;
            if (LockPopupPositionCheckBox != null) s.LockPopupPosition = LockPopupPositionCheckBox.IsChecked ?? false;
            s.OpenInCenterOnly = CenterPopupCheckBox.IsChecked ?? false;
            s.RightDoubleClickEnabled = RightDoubleClickCheckBox.IsChecked ?? true;
            s.SpaceQuickLookEnabled = SpaceQuickLookCheckBox.IsChecked ?? true;
            s.QuickLookHotkey = _currentQuickLookHotkey;
            s.ClosePopupHotkey = _currentClosePopupHotkey;
            s.CombineFilesOnDrop = CombineFilesCheckBox.IsChecked ?? true;
            s.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            s.AutoCheckUpdates = AutoCheckUpdatesCheckBox.IsChecked ?? true;
            s.GitHubRepo = GitHubUpdateService.TargetRepository;

            if (RandomShapeCheckBox != null) s.RandomShapeOnCreate = RandomShapeCheckBox.IsChecked ?? false;
            if (RandomColorCheckBox != null) s.RandomColorOnCreate = RandomColorCheckBox.IsChecked ?? false;
            if (RandomOpacityCheckBox != null) s.RandomOpacityOnCreate = RandomOpacityCheckBox.IsChecked ?? false;

            // Handle auto-start setting
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            InstallerService.SetAutoStart(exe, s.AutoStart);

            SettingsService.Instance.SaveSettings(s);
            PanakManager.Instance.UpdateOpenPopupThemeColor(s.ThemeColor);
            PanakManager.Instance.UpdateOpenPopupThemeMode(s.ThemeMode);
            PanakManager.Instance.UpdateOpenPopupOpacity(s.FolderOpacity);
            PanakManager.Instance.UpdateOpenPopupSettings();

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

        private void RandomShapeCheckBox_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.RandomShapeOnCreate = RandomShapeCheckBox.IsChecked ?? false;
            SettingsService.Instance.SaveSettings(s);
        }

        private void RandomColorCheckBox_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.RandomColorOnCreate = RandomColorCheckBox.IsChecked ?? false;
            SettingsService.Instance.SaveSettings(s);
        }

        private void RandomOpacityCheckBox_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.RandomOpacityOnCreate = RandomOpacityCheckBox.IsChecked ?? false;
            SettingsService.Instance.SaveSettings(s);
        }

        private void RandomShapeLabel_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                RandomShapeCheckBox.IsChecked = !(RandomShapeCheckBox.IsChecked ?? false);
                RandomShapeCheckBox_Click(RandomShapeCheckBox, new RoutedEventArgs());
            }
        }

        private void RandomColorLabel_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                RandomColorCheckBox.IsChecked = !(RandomColorCheckBox.IsChecked ?? false);
                RandomColorCheckBox_Click(RandomColorCheckBox, new RoutedEventArgs());
            }
        }

        private void RandomOpacityLabel_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                RandomOpacityCheckBox.IsChecked = !(RandomOpacityCheckBox.IsChecked ?? false);
                RandomOpacityCheckBox_Click(RandomOpacityCheckBox, new RoutedEventArgs());
            }
        }

        private void SettingsColorIndicator_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                var s = SettingsService.Instance.Current;
                if (!s.CustomPaletteColors.Contains(_selectedColor, StringComparer.OrdinalIgnoreCase)) {
                    s.CustomPaletteColors.Add(_selectedColor);
                    SettingsService.Instance.SaveSettings(s);
                    RebuildCustomSwatchesUI();
                }
            }
        }

        private void RebuildPresetSwatchesUI() {
            if (PresetSwatchesWrapPanel == null) return;
            PresetSwatchesWrapPanel.Children.Clear();
            var s = SettingsService.Instance.Current;
            string defaultPastel = ShellHelper.DefaultPastelColorHex;

            for (int i = 0; i < DefaultPresetColors.Length; i++) {
                var (hex, name) = DefaultPresetColors[i];
                if (i == 0) hex = defaultPastel;

                if (s.HiddenPresetColors != null && s.HiddenPresetColors.Contains(hex, StringComparer.OrdinalIgnoreCase)) {
                    continue;
                }

                var container = CreateSwatchContainer(hex, name, isCustom: false);
                if (i == 0 && container.Children.Count > 0 && container.Children[0] is RadioButton rb) {
                    SettingsAccentSwatch = rb;
                }
                PresetSwatchesWrapPanel.Children.Add(container);
            }
        }

        private void RebuildCustomSwatchesUI() {
            if (CustomSwatchesWrapPanel == null) return;
            CustomSwatchesWrapPanel.Children.Clear();
            var s = SettingsService.Instance.Current;
            if (s.CustomPaletteColors == null || s.CustomPaletteColors.Count == 0) return;

            foreach (var hex in s.CustomPaletteColors) {
                string colHex = hex;
                var container = CreateSwatchContainer(colHex, colHex, isCustom: true);
                CustomSwatchesWrapPanel.Children.Add(container);
            }
        }

        private Grid CreateSwatchContainer(string colHex, string tooltip, bool isCustom) {
            var grid = new Grid {
                Width = 28,
                Height = 28,
                Margin = new Thickness(3, 0, 3, 0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            var rb = new RadioButton {
                GroupName = "SettingsColorGroup",
                Style = (Style)FindResource("ColorSwatchStyle"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colHex)),
                Tag = colHex,
                ToolTip = tooltip,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (string.Equals(_selectedColor, colHex, StringComparison.OrdinalIgnoreCase)) {
                rb.IsChecked = true;
            }

            rb.Checked += (snd, ev) => {
                if (_isInitializing) return;
                _selectedColor = colHex;
                double hue = ShellHelper.GetHueFromHex(colHex);
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
            };

            grid.Children.Add(rb);

            // Delete badge in top-right corner
            var deleteBadge = new Border {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 30)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 0),
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand,
                ToolTip = LocalizationService.Instance["Delete"] ?? "Delete"
            };

            var crossPath = new System.Windows.Shapes.Path {
                Data = Geometry.Parse("M 3,3 L 9,9 M 9,3 L 3,9"),
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            deleteBadge.Child = crossPath;

            grid.MouseEnter += (s, e) => {
                deleteBadge.Visibility = Visibility.Visible;
            };
            grid.MouseLeave += (s, e) => {
                deleteBadge.Visibility = Visibility.Collapsed;
            };

            deleteBadge.MouseEnter += (s, e) => {
                deleteBadge.Background = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            };
            deleteBadge.MouseLeave += (s, e) => {
                deleteBadge.Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 30));
            };

            deleteBadge.PreviewMouseLeftButtonDown += (s, e) => {
                e.Handled = true; // Prevent checking the radio button
                var curSettings = SettingsService.Instance.Current;
                if (isCustom) {
                    curSettings.CustomPaletteColors.Remove(colHex);
                    SettingsService.Instance.SaveSettings(curSettings);
                    RebuildCustomSwatchesUI();
                } else {
                    if (!curSettings.HiddenPresetColors.Contains(colHex, StringComparer.OrdinalIgnoreCase)) {
                        curSettings.HiddenPresetColors.Add(colHex);
                        SettingsService.Instance.SaveSettings(curSettings);
                    }
                    grid.Visibility = Visibility.Collapsed;
                }

                if (string.Equals(_selectedColor, colHex, StringComparison.OrdinalIgnoreCase)) {
                    string defaultPastel = ShellHelper.DefaultPastelColorHex;
                    _selectedColor = defaultPastel;
                    double hue = ShellHelper.GetHueFromHex(defaultPastel);
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
                    var firstVisible = GetAllColorSwatchRadioButtons().FirstOrDefault();
                    if (firstVisible != null) firstVisible.IsChecked = true;
                }
            };

            grid.Children.Add(deleteBadge);
            return grid;
        }

        private void RestorePresetsMenuItem_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.HiddenPresetColors.Clear();
            SettingsService.Instance.SaveSettings(s);
            RebuildPresetSwatchesUI();
        }

        private IEnumerable<RadioButton> GetAllColorSwatchRadioButtons() {
            if (SettingsColorSwatchesPanel == null) yield break;
            foreach (var child in SettingsColorSwatchesPanel.Children) {
                if (child is RadioButton rb) {
                    yield return rb;
                } else if (child is Panel p) {
                    foreach (var pChild in p.Children) {
                        if (pChild is RadioButton prb) {
                            yield return prb;
                        } else if (pChild is Grid pg && pg.Children.Count > 0 && pg.Children[0] is RadioButton pgrb) {
                            if (pg.Visibility == Visibility.Visible) yield return pgrb;
                        }
                    }
                }
            }
        }

        private void ExportSettingsBtn_Click(object sender, RoutedEventArgs e) {
            var loc = LocalizationService.Instance;
            var dlg = new Microsoft.Win32.SaveFileDialog {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = "panak_settings.json",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() == true) {
                if (SettingsService.Instance.ExportSettings(dlg.FileName)) {
                    MessageBox.Show(loc["SettingsExported"] ?? "Settings exported successfully.", "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ImportSettingsBtn_Click(object sender, RoutedEventArgs e) {
            var loc = LocalizationService.Instance;
            var dlg = new Microsoft.Win32.OpenFileDialog {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() == true) {
                if (SettingsService.Instance.ImportSettings(dlg.FileName)) {
                    MessageBox.Show(loc["SettingsImported"] ?? "Settings imported successfully.", "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadCurrentSettings();
                    ApplyLocalization();
                    PanakManager.Instance.UpdateOpenPopupSettings();
                } else {
                    MessageBox.Show(loc["InvalidSettingsFile"] ?? "Invalid settings file.", "Panak", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        private void CrowdinButton_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://crowdin.com/project/panak") { UseShellExecute = true });
            } catch { }
        }

        private void CrowdinLink_MouseDown(object sender, MouseButtonEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://crowdin.com/project/panak") { UseShellExecute = true });
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
                UpdateWallpaperCrop();
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

        // --- Navigation Tabs & Smooth Scrolling ---
        private bool _isProgrammaticScrolling = false;

        private void NavTab_Click(object sender, RoutedEventArgs e) {
            if (sender is RadioButton tab) {
                FrameworkElement? target = tab.Name switch {
                    "NavTabAppearance" => SectionAppearance,
                    "NavTabLayout" => SectionLayout,
                    "NavTabSecurity" => SectionSecurity,
                    "NavTabSystem" => SectionSystem,
                    _ => null
                };
                if (target != null && SettingsScrollViewer != null) {
                    try {
                        if (tab.Name == "NavTabAppearance") {
                            AnimateScrollViewer(0);
                            return;
                        }
                        var transform = target.TransformToVisual(SettingsScrollViewer);
                        var position = transform.Transform(new Point(0, 0));
                        double targetOffset = SettingsScrollViewer.VerticalOffset + position.Y - 10;
                        if (targetOffset < 0) targetOffset = 0;
                        if (targetOffset > SettingsScrollViewer.ScrollableHeight) targetOffset = SettingsScrollViewer.ScrollableHeight;
                        AnimateScrollViewer(targetOffset);
                    } catch { }
                }
            }
        }

        private void AnimateScrollViewer(double toOffset) {
            if (SettingsScrollViewer == null) return;
            _isProgrammaticScrolling = true;
            double fromOffset = SettingsScrollViewer.VerticalOffset;
            int steps = 15;
            int currentStep = 0;
            var timer = new System.Windows.Threading.DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            timer.Tick += (s, e) => {
                currentStep++;
                double progress = (double)currentStep / steps;
                double eased = 1.0 - Math.Pow(1.0 - progress, 3);
                double newOffset = fromOffset + (toOffset - fromOffset) * eased;
                SettingsScrollViewer.ScrollToVerticalOffset(newOffset);
                if (currentStep >= steps) {
                    SettingsScrollViewer.ScrollToVerticalOffset(toOffset);
                    timer.Stop();
                    _isProgrammaticScrolling = false;
                }
            };
            timer.Start();
        }

        private void SettingsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_isProgrammaticScrolling || SettingsScrollViewer == null) return;

            try {
                // Calculate relative positions of each section header to determine active tab
                double viewTop = 60.0; // trigger line below header
                FrameworkElement[] sections = { SectionSystem, SectionSecurity, SectionLayout, SectionAppearance };
                RadioButton[] tabs = { NavTabSystem, NavTabSecurity, NavTabLayout, NavTabAppearance };

                for (int i = 0; i < sections.Length; i++) {
                    var sec = sections[i];
                    var tab = tabs[i];
                    if (sec != null && tab != null) {
                        var transform = sec.TransformToVisual(SettingsScrollViewer);
                        var pos = transform.Transform(new Point(0, 0));
                        if (pos.Y <= viewTop) {
                            if (tab.IsChecked != true) {
                                tab.IsChecked = true;
                            }
                            break;
                        }
                    }
                }
            } catch { }
        }

        // --- Preview Toggle & Resize ---
        private bool _isPreviewVisible = true;

        private void TogglePreviewBtn_Click(object sender, RoutedEventArgs e) {
            _isPreviewVisible = !_isPreviewVisible;
            var loc = LocalizationService.Instance;
            if (_isPreviewVisible) {
                if (PreviewCardBorder != null) PreviewCardBorder.Visibility = Visibility.Visible;
                if (SplitterColumn != null) SplitterColumn.Width = GridLength.Auto;
                if (PreviewSplitter != null) PreviewSplitter.Visibility = Visibility.Visible;
                if (PreviewColumn != null) {
                    PreviewColumn.MinWidth = 240;
                    PreviewColumn.MaxWidth = 460;
                    PreviewColumn.Width = new GridLength(340);
                }
                if (TogglePreviewBtnText != null) TogglePreviewBtnText.Text = loc["HidePreview"] ?? "Նախադիտում";
                AnimateWindowWidth(880);
                UpdateWallpaperCrop();
            } else {
                if (PreviewCardBorder != null) PreviewCardBorder.Visibility = Visibility.Collapsed;
                if (SplitterColumn != null) SplitterColumn.Width = new GridLength(0);
                if (PreviewSplitter != null) PreviewSplitter.Visibility = Visibility.Collapsed;
                if (PreviewColumn != null) {
                    PreviewColumn.MinWidth = 0;
                    PreviewColumn.MaxWidth = 0;
                    PreviewColumn.Width = new GridLength(0);
                }
                if (TogglePreviewBtnText != null) TogglePreviewBtnText.Text = loc["ShowPreview"] ?? "Նախադիտում";
                AnimateWindowWidth(520);
            }
        }

        private void AnimateWindowWidth(double toWidth) {
            try {
                var anim = new System.Windows.Media.Animation.DoubleAnimation {
                    To = toWidth,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                BeginAnimation(WidthProperty, anim);
            } catch {
                Width = toWidth;
            }
        }

        private int _previewCurrentPage = 0;
        private int _previewTotalPages = 1;
        private double _previewPageWidth = 234.0;
        private bool _isPreviewPinned = false;
        private bool _isPreviewEditOpen = false;
        private bool _isPreviewPopupOpen = true;
        private bool _isDraggingPreviewSlider = false;
        private Point _previewDragStartPos;

        private void PreviewCardBorder_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (PreviewClipRect != null) {
                PreviewClipRect.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
            int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
            int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
            bool isSlider = PagingSliderRadio?.IsChecked ?? true;
            UpdatePreviewCardDimensions(cols, rows, isSlider);
            UpdateWallpaperCrop();
        }

        // --- Live Desktop Preview System ---
        private BitmapSource? _fullWallpaper = null;

        private void StartWallpaperCapture() {
            try {
                _fullWallpaper = ShellHelper.GetDesktopWallpaperBitmap();
                UpdateWallpaperCrop();
            } catch { }
        }

        public void UpdateWallpaperCrop() {
            try {
                if (_fullWallpaper == null) {
                    _fullWallpaper = ShellHelper.GetDesktopWallpaperBitmap();
                    if (_fullWallpaper == null) return;
                }

                if (PreviewCardBorder == null || !IsLoaded || !_isPreviewVisible) {
                    if (PreviewWallpaperImage != null && PreviewWallpaperImage.Source != _fullWallpaper) {
                        PreviewWallpaperImage.Source = _fullWallpaper;
                    }
                    if (PreviewPopupBlurImage != null && PreviewPopupBlurImage.Source != _fullWallpaper) {
                        PreviewPopupBlurImage.Source = _fullWallpaper;
                    }
                    return;
                }

                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                try {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                    dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                } catch { }

                Point screenPt = PreviewCardBorder.PointToScreen(new Point(0, 0));
                double pxW = (PreviewCardBorder.ActualWidth > 0 ? PreviewCardBorder.ActualWidth : 340) * dpiScaleX;
                double pxH = (PreviewCardBorder.ActualHeight > 0 ? PreviewCardBorder.ActualHeight : 600) * dpiScaleY;

                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Rect monBounds = ShellHelper.GetMonitorBounds(hwnd);

                double monW = monBounds.Width > 0 ? monBounds.Width : SystemParameters.PrimaryScreenWidth * dpiScaleX;
                double monH = monBounds.Height > 0 ? monBounds.Height : SystemParameters.PrimaryScreenHeight * dpiScaleY;

                double relX = screenPt.X - monBounds.Left;
                double relY = screenPt.Y - monBounds.Top;

                double scaleX = (double)_fullWallpaper.PixelWidth / monW;
                double scaleY = (double)_fullWallpaper.PixelHeight / monH;

                int cropX = (int)Math.Round(relX * scaleX);
                int cropY = (int)Math.Round(relY * scaleY);
                int cropW = (int)Math.Round(pxW * scaleX);
                int cropH = (int)Math.Round(pxH * scaleY);

                if (cropX < 0) { cropW += cropX; cropX = 0; }
                if (cropY < 0) { cropH += cropY; cropY = 0; }
                if (cropX + cropW > _fullWallpaper.PixelWidth) cropW = _fullWallpaper.PixelWidth - cropX;
                if (cropY + cropH > _fullWallpaper.PixelHeight) cropH = _fullWallpaper.PixelHeight - cropY;

                if (cropW > 20 && cropH > 20) {
                    var cropped = new CroppedBitmap(_fullWallpaper, new Int32Rect(cropX, cropY, cropW, cropH));
                    cropped.Freeze();
                    if (PreviewWallpaperImage != null) PreviewWallpaperImage.Source = cropped;
                    if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = cropped;
                } else {
                    if (PreviewWallpaperImage != null) PreviewWallpaperImage.Source = _fullWallpaper;
                    if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = _fullWallpaper;
                }
            } catch {
                if (_fullWallpaper != null) {
                    if (PreviewWallpaperImage != null) PreviewWallpaperImage.Source = _fullWallpaper;
                    if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = _fullWallpaper;
                }
            }
        }

        private static ImageSource? _cachedFolderMockIcon = null;
        private static readonly Dictionary<string, ImageSource> _cachedMockIconSources = new(StringComparer.OrdinalIgnoreCase);

        private static ImageSource? GetPreviewMockIcon(bool isFolder, string ext) {
            try {
                if (isFolder) {
                    if (_cachedFolderMockIcon != null) return _cachedFolderMockIcon;
                    _cachedFolderMockIcon = ShellHelper.GetFolderIconSource(isLarge: true);
                    return _cachedFolderMockIcon;
                }
                if (!string.IsNullOrEmpty(ext)) {
                    if (_cachedMockIconSources.TryGetValue(ext, out var cached)) return cached;
                    var src = ShellHelper.GetFileIconSource("test" + ext, isLarge: true);
                    if (src != null) {
                        _cachedMockIconSources[ext] = src;
                        return src;
                    }
                }
            } catch { }
            return null;
        }

        // Realistic localized sample items matching real desktop folders (Image 1 parity)
        private static readonly (string Name, string Ext, bool IsFolder)[] SamplePreviewItemsArm = new[] {
            ("data", "", true),
            ("dist", "", true),
            ("node_modules", "", true),
            ("public", "", true),
            ("src", "", true),
            ("stitch_modern", "", true),
            ("test", "", true),
            ("views", "", true),
            (".env", ".env", false),
            ("Զեկույց.pdf", ".pdf", false),
            ("Աղյուսակ.xlsx", ".xlsx", false),
            ("Սլայդներ.pptx", ".pptx", false),
            ("Լոգո.png", ".png", false)
        };

        private static readonly (string Name, string Ext, bool IsFolder)[] SamplePreviewItemsEn = new[] {
            ("data", "", true),
            ("dist", "", true),
            ("node_modules", "", true),
            ("public", "", true),
            ("src", "", true),
            ("stitch_modern", "", true),
            ("test", "", true),
            ("views", "", true),
            (".env", ".env", false),
            ("Report.pdf", ".pdf", false),
            ("Spreadsheet.xlsx", ".xlsx", false),
            ("Slides.pptx", ".pptx", false),
            ("Logo.png", ".png", false)
        };

        private static readonly (string Name, string Ext, bool IsFolder)[] SamplePreviewItemsRu = new[] {
            ("data", "", true),
            ("dist", "", true),
            ("node_modules", "", true),
            ("public", "", true),
            ("src", "", true),
            ("stitch_modern", "", true),
            ("test", "", true),
            ("views", "", true),
            (".env", ".env", false),
            ("Отчет.pdf", ".pdf", false),
            ("Таблица.xlsx", ".xlsx", false),
            ("Презентация.pptx", ".pptx", false),
            ("Логотип.png", ".png", false)
        };

        private (string Name, string Ext, bool IsFolder)[] GetCurrentSampleItems() {
            string lang = LocalizationService.Instance.CurrentLanguage;
            if (string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)) return SamplePreviewItemsEn;
            if (string.Equals(lang, "ru", StringComparison.OrdinalIgnoreCase)) return SamplePreviewItemsRu;
            return SamplePreviewItemsArm;
        }

        private void UpdateLivePreview() {
            try {
                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());

                var textColor = isDark ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(0, 0, 0);
                var subtextColor = isDark ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(0, 0, 0);

                Resources["PopupTextBrush"] = new SolidColorBrush(textColor);
                Resources["PopupSubtextBrush"] = new SolidColorBrush(subtextColor);

                if (isDark) {
                    if (PreviewCardBorderInner != null) {
                        PreviewCardBorderInner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(56, 255, 255, 255));
                        PreviewCardBorderInner.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
                    }
                    if (PreviewFolderTitleText != null) PreviewFolderTitleText.Foreground = Brushes.White;
                    if (PreviewEditNameLabel != null) PreviewEditNameLabel.Foreground = Brushes.White;
                    if (PreviewColorThemeLabel != null) PreviewColorThemeLabel.Foreground = Brushes.White;
                    if (PreviewFolderLockHeaderLabel != null) PreviewFolderLockHeaderLabel.Foreground = Brushes.White;
                    if (PreviewAlwaysPanakCheckBox != null) PreviewAlwaysPanakCheckBox.Foreground = Brushes.White;

                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupHeaderBtnFg"] = Brushes.White;
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupItemBadgeFg"] = Brushes.White;
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 255, 255, 255));
                } else {
                    if (PreviewCardBorderInner != null) {
                        PreviewCardBorderInner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0));
                        PreviewCardBorderInner.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255));
                    }
                    if (PreviewFolderTitleText != null) PreviewFolderTitleText.Foreground = Brushes.Black;
                    if (PreviewEditNameLabel != null) PreviewEditNameLabel.Foreground = Brushes.Black;
                    if (PreviewColorThemeLabel != null) PreviewColorThemeLabel.Foreground = Brushes.Black;
                    if (PreviewFolderLockHeaderLabel != null) PreviewFolderLockHeaderLabel.Foreground = Brushes.Black;
                    if (PreviewAlwaysPanakCheckBox != null) PreviewAlwaysPanakCheckBox.Foreground = Brushes.Black;

                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 0, 0, 0));
                    Resources["PopupHeaderBtnFg"] = Brushes.Black;
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                    Resources["PopupItemBadgeFg"] = Brushes.Black;
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                }

                // Frosted Tint Layer (dark/light)
                bool hasBlur = FolderBlurCheckBox?.IsChecked ?? true;
                if (PreviewFrostedTintLayer != null) {
                    if (isDark) {
                        PreviewFrostedTintLayer.Background = new SolidColorBrush(hasBlur
                            ? System.Windows.Media.Color.FromArgb(194, 22, 22, 29)
                            : System.Windows.Media.Color.FromArgb(235, 24, 24, 30));
                    } else {
                        PreviewFrostedTintLayer.Background = new SolidColorBrush(hasBlur
                            ? System.Windows.Media.Color.FromArgb(220, 248, 249, 250)
                            : System.Windows.Media.Color.FromArgb(245, 248, 249, 250));
                    }
                }

                // Accent Tint Layer
                System.Windows.Media.Color baseCol;
                try {
                    baseCol = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_selectedColor);
                } catch {
                    baseCol = System.Windows.Media.Color.FromRgb(252, 221, 197);
                }

                if (PreviewTintLayer != null) {
                    PreviewTintLayer.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(95, baseCol.R, baseCol.G, baseCol.B));
                }

                // Opacity syncing
                double opacityVal = GeneralFolderOpacitySlider != null ? GeneralFolderOpacitySlider.Value : 85.0;
                double normOpacity = Math.Clamp(opacityVal / 100.0, 0.20, 1.0);
                if (PreviewFrostedTintLayer != null) PreviewFrostedTintLayer.Opacity = normOpacity;
                if (PreviewTintLayer != null) PreviewTintLayer.Opacity = Math.Min(1.0, normOpacity * 1.1);

                // Blur effect
                if (PreviewPopupBlurEffect != null && FolderBlurSlider != null) {
                    PreviewPopupBlurEffect.Radius = Math.Max(2, FolderBlurSlider.Value);
                }
                if (PreviewPopupBlurImage != null) {
                    PreviewPopupBlurImage.Visibility = hasBlur ? Visibility.Visible : Visibility.Collapsed;
                }

                // Scrollbar thumb dynamic theme color
                var thumbColor = System.Windows.Media.Color.FromArgb(150, baseCol.R, baseCol.G, baseCol.B);
                var hoverColor = System.Windows.Media.Color.FromArgb(220, baseCol.R, baseCol.G, baseCol.B);
                Resources["PanakScrollBarThumbBrush"] = new SolidColorBrush(thumbColor);
                Resources["PanakScrollBarThumbHoverBrush"] = new SolidColorBrush(hoverColor);

                // Material 3 Dynamic shape capsule styling
                double hue = ShellHelper.GetHueFromHex(_selectedColor);
                var (surfaceHex, containerHex, primaryHex) = ShellHelper.GetMaterialYouTonalPaletteHex(hue);
                var primaryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(primaryHex);
                var containerColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(containerHex);

                var capsuleBg = System.Windows.Media.Color.FromArgb(45, primaryColor.R, primaryColor.G, primaryColor.B);
                var unselectedBg = System.Windows.Media.Color.FromArgb(50, containerColor.R, containerColor.G, containerColor.B);
                Resources["ShapeCapsuleBgBrush"] = new SolidColorBrush(capsuleBg);
                Resources["ShapeUnselectedBgBrush"] = new SolidColorBrush(unselectedBg);
                Resources["ShapeSelectedBgBrush"] = new SolidColorBrush(primaryColor);
                Resources["ShapeUnselectedPathBrush"] = isDark ? Brushes.White : Brushes.Black;
                Resources["ShapeSelectedPathBrush"] = Brushes.White;
                Resources["CurrentHueThumbBrush"] = new SolidColorBrush(baseCol);

                if (PreviewHueSlider != null && Math.Abs(PreviewHueSlider.Value - hue) > 1.0) {
                    PreviewHueSlider.Value = hue;
                }
                if (PreviewOpacitySlider != null && Math.Abs(PreviewOpacitySlider.Value - normOpacity) > 0.02) {
                    PreviewOpacitySlider.Value = normOpacity;
                }

                // Desktop folder icon at bottom left
                if (PreviewDesktopIconShapePath != null) {
                    Geometry? geom = _selectedShape switch {
                        "Circle" => FindResource("GeomCircle") as Geometry,
                        "Clover" => FindResource("GeomClover") as Geometry,
                        "Scallop" => FindResource("GeomScallop") as Geometry,
                        "Arch" => FindResource("GeomArch") as Geometry,
                        _ => FindResource("GeomSquircle") as Geometry
                    };
                    if (geom != null) PreviewDesktopIconShapePath.Data = geom;
                    byte iconAlpha = (byte)Math.Clamp((int)Math.Round(normOpacity * 255.0), 30, 255);
                    PreviewDesktopIconShapePath.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(iconAlpha, baseCol.R, baseCol.G, baseCol.B));
                    PreviewDesktopIconShapePath.Opacity = normOpacity;
                }

                UpdatePreviewPinVisual();
                PopulatePreviewDesktopIcon();
                PopulatePreviewItems();
            } catch { }
        }

        private void PopulatePreviewDesktopIcon() {
            if (PreviewDesktopIconMiniGrid == null) return;
            if (PreviewDesktopIconMiniGrid.Children.Count == 0) {
                var samples = GetCurrentSampleItems();
                for (int i = 0; i < 4; i++) {
                    var item = samples[i % samples.Length];
                    var iconSrc = GetPreviewMockIcon(item.IsFolder, item.Ext);
                    var img = new Image {
                        Source = iconSrc,
                        Width = 12,
                        Height = 12,
                        Margin = new Thickness(1)
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    PreviewDesktopIconMiniGrid.Children.Add(img);
                }
            }
        }

        private Border CreatePreviewItemView((string Name, string Ext, bool IsFolder) sample) {
            var itemBorder = new Border {
                Width = 68,
                Height = 76,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Margin = new Thickness(5),
                Padding = new Thickness(2, 4, 2, 4),
                Cursor = Cursors.Hand
            };

            itemBorder.MouseEnter += (s, e) => {
                itemBorder.Background = TryFindResource("PopupItemHoverBrush") as Brush ?? new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 255, 255, 255));
            };
            itemBorder.MouseLeave += (s, e) => {
                itemBorder.Background = Brushes.Transparent;
            };

            var stack = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 66
            };

            var iconGrid = new Grid {
                Width = 47,
                Height = 47,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var iconSrc = GetPreviewMockIcon(sample.IsFolder, sample.Ext);
            var img = new Image {
                Source = iconSrc,
                Width = 43,
                Height = 43,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            iconGrid.Children.Add(img);
            stack.Children.Add(iconGrid);

            var txt = new TextBlock {
                Text = sample.Name,
                FontSize = 11,
                Foreground = TryFindResource("PopupTextBrush") as Brush ?? Brushes.White,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 26,
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = sample.Name
            };
            stack.Children.Add(txt);
            itemBorder.Child = stack;
            return itemBorder;
        }

        private void PopulatePreviewItems() {
            var samples = GetCurrentSampleItems();
            int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
            int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
            bool isSlider = PagingSliderRadio?.IsChecked ?? true;

            double itemW = 78.0;
            double itemH = 86.0;
            double pageW = cols * itemW;
            double pageH = rows * itemH;
            _previewPageWidth = pageW;

            int itemsPerPage = Math.Max(1, cols * rows);
            _previewTotalPages = (int)Math.Ceiling((double)samples.Length / itemsPerPage);
            if (_previewTotalPages < 1) _previewTotalPages = 1;
            if (_previewCurrentPage >= _previewTotalPages) _previewCurrentPage = _previewTotalPages - 1;
            if (_previewCurrentPage < 0) _previewCurrentPage = 0;

            // 1. Slider pages setup
            if (PreviewSliderPagesViewport != null && PreviewSliderPagesPanel != null) {
                PreviewSliderPagesViewport.Width = pageW;
                PreviewSliderPagesViewport.Height = pageH;
                PreviewSliderPagesPanel.Width = _previewTotalPages * pageW;
                PreviewSliderPagesPanel.Height = pageH;
                PreviewSliderPagesPanel.Children.Clear();

                for (int p = 0; p < _previewTotalPages; p++) {
                    var pageGrid = new UniformGrid {
                        Columns = cols,
                        Rows = rows,
                        Width = pageW,
                        Height = pageH,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var pageItems = samples.Skip(p * itemsPerPage).Take(itemsPerPage);
                    foreach (var item in pageItems) {
                        pageGrid.Children.Add(CreatePreviewItemView(item));
                    }
                    PreviewSliderPagesPanel.Children.Add(pageGrid);
                }

                AnimatePreviewSliderPage(immediate: true);
            }

            // 2. ScrollViewer setup
            if (PreviewScrollGrid != null) {
                PreviewScrollGrid.Columns = cols;
                PreviewScrollGrid.Width = pageW;
                PreviewScrollGrid.Children.Clear();
                foreach (var item in samples) {
                    PreviewScrollGrid.Children.Add(CreatePreviewItemView(item));
                }
            }

            if (PreviewItemsScrollViewer != null) {
                PreviewItemsScrollViewer.MaxHeight = Math.Min(rows, 3) * itemH + 8;
            }

            // 3. Switch between Slider and Scrollbar mode
            if (PreviewSliderPagesViewport != null && PreviewItemsScrollViewer != null && PreviewFooterPagerGrid != null) {
                if (isSlider) {
                    PreviewSliderPagesViewport.Visibility = Visibility.Visible;
                    PreviewItemsScrollViewer.Visibility = Visibility.Collapsed;
                    PreviewFooterPagerGrid.Visibility = (_previewTotalPages > 1) ? Visibility.Visible : Visibility.Collapsed;
                    UpdatePreviewDots();
                } else {
                    PreviewSliderPagesViewport.Visibility = Visibility.Collapsed;
                    PreviewItemsScrollViewer.Visibility = Visibility.Visible;
                    PreviewFooterPagerGrid.Visibility = Visibility.Collapsed;
                }
            }

            // 4. Update count badge
            if (PreviewItemCountBadge != null) {
                string countFormatted = $"{samples.Length} {LocalizationService.Instance["FilesCount"]}";
                PreviewItemCountBadge.Text = countFormatted;
            }

            // 5. Update layout sizing and scaling
            UpdatePreviewCardDimensions(cols, rows, isSlider);
        }

        private void UpdatePreviewCardDimensions(int cols, int rows, bool isSlider) {
            if (PreviewCardBorderInner == null || PreviewInnerClippingBorder == null || PreviewCardClipGeometry == null) return;

            double naturalW = cols * 78.0 + 24.0;
            double naturalH = (rows * 86.0) + 36.0 /* header */ + (isSlider && _previewTotalPages > 1 ? 30.0 : 0.0) /* footer */ + 22.0 /* inner padding */;
            if (_isPreviewEditOpen && PreviewEditPanelContainer != null) {
                double editH = PreviewEditPanelContainer.ActualHeight > 0 ? PreviewEditPanelContainer.ActualHeight : 240.0;
                naturalH += editH;
            }

            PreviewCardBorderInner.Width = naturalW;
            PreviewCardBorderInner.Height = naturalH;
            PreviewInnerClippingBorder.Width = naturalW;
            PreviewInnerClippingBorder.Height = naturalH;
            PreviewCardClipGeometry.Rect = new Rect(0, 0, naturalW, naturalH);

            if (PreviewPopupBlurImage != null) {
                PreviewPopupBlurImage.Width = naturalW;
                PreviewPopupBlurImage.Height = naturalH;
            }

            double availW = Math.Max(240, PreviewCardBorder.ActualWidth > 0 ? PreviewCardBorder.ActualWidth - 28.0 : 312.0);
            double availH = Math.Max(300, PreviewCardBorder.ActualHeight > 0 ? PreviewCardBorder.ActualHeight - 85.0 : 445.0);

            double scale = 1.0;
            if (naturalW > availW || naturalH > availH) {
                scale = Math.Min(availW / naturalW, availH / naturalH);
            }
            if (scale > 1.0) scale = 1.0;

            if (PreviewPopupScale != null) {
                PreviewPopupScale.ScaleX = scale;
                PreviewPopupScale.ScaleY = scale;
            }
        }

        private void UpdatePreviewDots() {
            if (PreviewSliderDotsPanel == null) return;
            PreviewSliderDotsPanel.Children.Clear();
            if (_previewTotalPages <= 1) return;

            var textBrush = TryFindResource("PopupTextBrush") as Brush ?? Brushes.White;
            var subtextBrush = TryFindResource("PopupSubtextBrush") as Brush ?? Brushes.Gray;

            for (int i = 0; i < _previewTotalPages; i++) {
                int pageIndex = i;
                bool isActive = (i == _previewCurrentPage);
                var dot = new Border {
                    Width = isActive ? 20.0 : 7.0,
                    Height = 7.0,
                    CornerRadius = new CornerRadius(3.5),
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    Background = isActive ? textBrush : subtextBrush,
                    Opacity = isActive ? 0.95 : 0.45,
                    Cursor = Cursors.Hand,
                    Tag = pageIndex
                };
                dot.MouseLeftButtonDown += (s, e) => {
                    _previewCurrentPage = pageIndex;
                    AnimatePreviewSliderPage(immediate: false);
                    UpdatePreviewDots();
                };
                PreviewSliderDotsPanel.Children.Add(dot);
            }

            bool arrowsEnabled = SliderArrowsCheckBox?.IsChecked ?? true;
            if (PreviewSliderPrevBtn != null) {
                PreviewSliderPrevBtn.Visibility = arrowsEnabled ? Visibility.Visible : Visibility.Collapsed;
                PreviewSliderPrevBtn.IsEnabled = _previewCurrentPage > 0;
                PreviewSliderPrevBtn.Opacity = _previewCurrentPage > 0 ? 1.0 : 0.35;
            }
            if (PreviewSliderNextBtn != null) {
                PreviewSliderNextBtn.Visibility = arrowsEnabled ? Visibility.Visible : Visibility.Collapsed;
                PreviewSliderNextBtn.IsEnabled = _previewCurrentPage < _previewTotalPages - 1;
                PreviewSliderNextBtn.Opacity = _previewCurrentPage < _previewTotalPages - 1 ? 1.0 : 0.35;
            }
        }

        private void AnimatePreviewSliderPage(bool immediate) {
            if (PreviewPagesTranslateX == null) return;
            double targetX = -_previewCurrentPage * _previewPageWidth;
            if (immediate) {
                PreviewPagesTranslateX.BeginAnimation(TranslateTransform.XProperty, null);
                PreviewPagesTranslateX.X = targetX;
            } else {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var anim = new DoubleAnimation {
                    To = targetX,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };
                PreviewPagesTranslateX.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }

        private void PreviewSliderPrevBtn_Click(object sender, RoutedEventArgs e) {
            if (_previewCurrentPage > 0) {
                _previewCurrentPage--;
                AnimatePreviewSliderPage(immediate: false);
                UpdatePreviewDots();
            }
        }

        private void PreviewSliderNextBtn_Click(object sender, RoutedEventArgs e) {
            if (_previewCurrentPage < _previewTotalPages - 1) {
                _previewCurrentPage++;
                AnimatePreviewSliderPage(immediate: false);
                UpdatePreviewDots();
            }
        }

        private void PreviewSlider_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                _isDraggingPreviewSlider = true;
                _previewDragStartPos = e.GetPosition(PreviewSliderPagesViewport);
                PreviewSliderPagesViewport?.CaptureMouse();
            }
        }

        private void PreviewSlider_MouseMove(object sender, MouseEventArgs e) {
            if (_isDraggingPreviewSlider && e.LeftButton == MouseButtonState.Pressed) {
                var currentPos = e.GetPosition(PreviewSliderPagesViewport);
                double deltaX = currentPos.X - _previewDragStartPos.X;
                if (Math.Abs(deltaX) > 40) {
                    _isDraggingPreviewSlider = false;
                    PreviewSliderPagesViewport?.ReleaseMouseCapture();
                    if (deltaX < 0 && _previewCurrentPage < _previewTotalPages - 1) {
                        _previewCurrentPage++;
                        AnimatePreviewSliderPage(immediate: false);
                        UpdatePreviewDots();
                    } else if (deltaX > 0 && _previewCurrentPage > 0) {
                        _previewCurrentPage--;
                        AnimatePreviewSliderPage(immediate: false);
                        UpdatePreviewDots();
                    }
                }
            }
        }

        private void PreviewSlider_MouseUp(object sender, MouseButtonEventArgs e) {
            _isDraggingPreviewSlider = false;
            PreviewSliderPagesViewport?.ReleaseMouseCapture();
        }

        private void PreviewSlider_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (_previewTotalPages <= 1) return;
            if (e.Delta < 0) {
                if (_previewCurrentPage < _previewTotalPages - 1) {
                    _previewCurrentPage++;
                    AnimatePreviewSliderPage(immediate: false);
                    UpdatePreviewDots();
                    e.Handled = true;
                }
            } else if (e.Delta > 0) {
                if (_previewCurrentPage > 0) {
                    _previewCurrentPage--;
                    AnimatePreviewSliderPage(immediate: false);
                    UpdatePreviewDots();
                    e.Handled = true;
                }
            }
        }

        private void PreviewScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (PreviewItemsScrollViewer != null) {
                if (e.Delta < 0) {
                    PreviewItemsScrollViewer.LineDown();
                } else {
                    PreviewItemsScrollViewer.LineUp();
                }
                e.Handled = true;
            }
        }

        private void PreviewPinButton_Click(object sender, RoutedEventArgs e) {
            _isPreviewPinned = !_isPreviewPinned;
            UpdatePreviewPinVisual();
        }

        private void UpdatePreviewPinVisual() {
            if (PreviewPinIconPath == null) return;
            if (_isPreviewPinned) {
                PreviewPinIconPath.Fill = (Brush)FindResource("M3PrimaryBrush");
                if (PreviewPinButton != null) {
                    PreviewPinButton.ToolTip = "Ապաամրացնել";
                }
            } else {
                PreviewPinIconPath.Fill = (Brush)FindResource("PopupHeaderBtnFg");
                if (PreviewPinButton != null) {
                    PreviewPinButton.ToolTip = "Ամրացնել";
                }
            }
        }

        private void PreviewEditButton_Click(object sender, RoutedEventArgs e) {
            if (PreviewEditPanelContainer == null || PreviewEditPanel == null) return;

            _isPreviewEditOpen = !_isPreviewEditOpen;
            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            if (_isPreviewEditOpen) {
                PreviewEditPanelContainer.Visibility = Visibility.Visible;
                PreviewEditPanel.Measure(new Size(234, double.PositiveInfinity));
                double targetH = PreviewEditPanel.DesiredSize.Height > 0 ? PreviewEditPanel.DesiredSize.Height : 240.0;

                var hAnim = new DoubleAnimation(0.0, targetH, duration) { EasingFunction = ease };
                var opAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = ease };
                var trAnim = new DoubleAnimation(-10.0, 0.0, duration) { EasingFunction = ease };

                PreviewEditPanelContainer.BeginAnimation(HeightProperty, hAnim);
                PreviewEditPanel.BeginAnimation(OpacityProperty, opAnim);
                PreviewEditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, trAnim);
            } else {
                double startH = PreviewEditPanelContainer.ActualHeight > 0 ? PreviewEditPanelContainer.ActualHeight : PreviewEditPanelContainer.Height;
                var hAnim = new DoubleAnimation(startH, 0.0, duration) { EasingFunction = ease };
                var opAnim = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = ease };
                var trAnim = new DoubleAnimation(0.0, -10.0, duration) { EasingFunction = ease };

                hAnim.Completed += (s, ev) => {
                    if (!_isPreviewEditOpen) {
                        PreviewEditPanelContainer.Visibility = Visibility.Collapsed;
                    }
                };

                PreviewEditPanelContainer.BeginAnimation(HeightProperty, hAnim);
                PreviewEditPanel.BeginAnimation(OpacityProperty, opAnim);
                PreviewEditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, trAnim);
            }

            int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
            int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
            bool isSlider = PagingSliderRadio?.IsChecked ?? true;
            UpdatePreviewCardDimensions(cols, rows, isSlider);
        }

        private void PreviewExplorerButton_Click(object sender, RoutedEventArgs e) {
            try {
                var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                ShellContextMenu.OpenFile(defaultPath);
            } catch { }
        }

        private void PreviewCloseButton_Click(object sender, RoutedEventArgs e) {
            if (PreviewPopupContainer == null || !_isPreviewPopupOpen) return;
            _isPreviewPopupOpen = false;

            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var scaleXAnim = new DoubleAnimation(PreviewPopupScale.ScaleX, 0.0, duration) { EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation(PreviewPopupScale.ScaleY, 0.0, duration) { EasingFunction = ease };
            var opAnim = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = ease };

            scaleXAnim.Completed += (s, ev) => {
                if (!_isPreviewPopupOpen) {
                    PreviewPopupContainer.Visibility = Visibility.Collapsed;
                }
            };

            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            PreviewPopupContainer.BeginAnimation(OpacityProperty, opAnim);
        }

        private void PreviewDesktopIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (PreviewPopupContainer == null) return;
            if (!_isPreviewPopupOpen) {
                _isPreviewPopupOpen = true;
                PreviewPopupContainer.Visibility = Visibility.Visible;

                int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
                int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
                bool isSlider = PagingSliderRadio?.IsChecked ?? true;
                UpdatePreviewCardDimensions(cols, rows, isSlider);

                double targetScale = PreviewPopupScale.ScaleX > 0.1 ? PreviewPopupScale.ScaleX : 1.0;
                PreviewPopupScale.ScaleX = 0.0;
                PreviewPopupScale.ScaleY = 0.0;
                PreviewPopupContainer.Opacity = 0.0;

                var duration = TimeSpan.FromMilliseconds(260);
                var ease = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };

                var scaleXAnim = new DoubleAnimation(0.0, targetScale, duration) { EasingFunction = ease };
                var scaleYAnim = new DoubleAnimation(0.0, targetScale, duration) { EasingFunction = ease };
                var opAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

                PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
                PreviewPopupContainer.BeginAnimation(OpacityProperty, opAnim);
            } else {
                PreviewCloseButton_Click(sender, e);
            }
        }

        private void PreviewSaveNameButton_Click(object sender, RoutedEventArgs e) {
            if (PreviewNameEditBox != null && PreviewFolderTitleText != null) {
                PreviewFolderTitleText.Text = PreviewNameEditBox.Text;
                if (PreviewDesktopIconLabel != null) {
                    PreviewDesktopIconLabel.Text = PreviewNameEditBox.Text;
                }
            }
        }

        private void PreviewNameEditBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                PreviewSaveNameButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void PreviewColorSwatch_Checked(object sender, RoutedEventArgs e) {
            if (sender is RadioButton rb && rb.Tag is string hex) {
                _selectedColor = hex;
                double hue = ShellHelper.GetHueFromHex(hex);
                if (SettingsHueSlider != null) {
                    SettingsHueSlider.Value = hue;
                }
                UpdateLivePreview();
            }
        }

        private void PreviewHueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (SettingsHueSlider != null && Math.Abs(SettingsHueSlider.Value - e.NewValue) > 1.0) {
                SettingsHueSlider.Value = e.NewValue;
            }
            ApplyMaterialYouPalette(e.NewValue);
            UpdateLivePreview();
        }

        private void PreviewOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GeneralFolderOpacitySlider != null && Math.Abs(GeneralFolderOpacitySlider.Value - (e.NewValue * 100.0)) > 1.0) {
                GeneralFolderOpacitySlider.Value = e.NewValue * 100.0;
            }
            UpdateLivePreview();
        }

        private void PreviewShapeRadio_Click(object sender, RoutedEventArgs e) {
            string chosenShape = "Squircle";
            if (sender == PreviewShapeRadioCircle) chosenShape = "Circle";
            else if (sender == PreviewShapeRadioSquircle) chosenShape = "Squircle";
            else if (sender == PreviewShapeRadioClover) chosenShape = "Clover";
            else if (sender == PreviewShapeRadioScallop) chosenShape = "Scallop";
            else if (sender == PreviewShapeRadioArch) chosenShape = "Arch";

            _selectedShape = chosenShape;
            if (chosenShape == "Circle" && SettingsShapeCircle != null) SettingsShapeCircle.IsChecked = true;
            else if (chosenShape == "Squircle" && SettingsShapeSquircle != null) SettingsShapeSquircle.IsChecked = true;
            else if (chosenShape == "Clover" && SettingsShapeClover != null) SettingsShapeClover.IsChecked = true;
            else if (chosenShape == "Scallop" && SettingsShapeScallop != null) SettingsShapeScallop.IsChecked = true;
            else if (chosenShape == "Arch" && SettingsShapeArch != null) SettingsShapeArch.IsChecked = true;

            UpdateLivePreview();
        }

        private void PreviewAlwaysPanakCheckBox_Checked(object sender, RoutedEventArgs e) { }
        private void PreviewAlwaysPanakCheckBox_Unchecked(object sender, RoutedEventArgs e) { }
        private void PreviewUnpackFolderButton_Click(object sender, RoutedEventArgs e) { }
        private void PreviewFolderLockBtn_Click(object sender, RoutedEventArgs e) {
            var win = new FolderLockSetupWindow("__PREVIEW__");
            win.Owner = this;
            win.ShowDialog();
        }

        // --- Settings Lock & Biometrics Enrollment ---
        private void CheckSettingsLockState() {
            if (FolderLockService.Instance.IsSettingsLocked) {
                if (SettingsLockOverlay != null) {
                    SettingsLockOverlay.Visibility = Visibility.Visible;
                    SettingsLockOverlay.SetupForSettings();
                    SettingsLockOverlay.Unlocked += () => {
                        SettingsLockOverlay.Visibility = Visibility.Collapsed;
                    };
                }
            } else {
                if (SettingsLockOverlay != null) {
                    SettingsLockOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdateSettingsLockUI() {
            bool isLocked = FolderLockService.Instance.IsSettingsLocked;
            if (LockSettingsCheckBox != null) {
                LockSettingsCheckBox.IsChecked = isLocked;
            }
            if (SettingsLockStatusText != null) {
                if (isLocked) {
                    string typeArm = FolderLockService.Instance.SettingsLockType switch {
                        "PIN" => "PIN կոդ",
                        "Pattern" => "Նախշ",
                        _ => "Գաղտնաբառ"
                    };
                    SettingsLockStatusText.Text = $"🔒 Կողպված է ({typeArm})";
                    SettingsLockStatusText.Foreground = (Brush)FindResource("M3PrimaryBrush");
                } else {
                    SettingsLockStatusText.Text = "Կայանքները պաշտպանված չեն";
                    SettingsLockStatusText.Foreground = (Brush)FindResource("M3OnSurfaceVariantBrush");
                }
            }
        }

        private void LockSettingsCheckBox_Click(object sender, RoutedEventArgs e) {
            var win = new FolderLockSetupWindow("__SETTINGS__");
            win.Owner = this;
            win.ShowDialog();
            UpdateSettingsLockUI();
        }

        private void ConfigureSettingsLockBtn_Click(object sender, RoutedEventArgs e) {
            var win = new FolderLockSetupWindow("__SETTINGS__");
            win.Owner = this;
            win.ShowDialog();
            UpdateSettingsLockUI();
        }
    }
}
