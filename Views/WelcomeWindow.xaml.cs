using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Panak.Models;
using Panak.Services;

namespace Panak.Views {
    public partial class WelcomeWindow : Window {
        private int _currentStep = 0;
        private bool _isInitializing = true;

        private string _selectedShape = "Squircle";
        private string _selectedColor = ShellHelper.DefaultPastelColorHex;
        private double _folderOpacity = 0.85;
        private bool _folderBlurEnabled = true;
        private double _folderBlurRadius = 28.0;

        private string _pagingMode = "Slider";
        private int _gridColumns = 5;
        private int _gridRows = 3;
        private bool _sliderArrows = true;
        private bool _sliderDrag = true;
        private bool _focusMode = true;
        private double _focusDimming = 0.45;
        private bool _focusBlurEnabled = true;
        private double _focusBlurRadius = 28.0;

        private DispatcherTimer? _liveWallpaperTimer;
        private bool _isPopup1Open = true;
        private bool _isPopup2Open = false;

        private int _currentPreviewPage = 0;
        private int _totalPreviewPages = 3;
        private double _previewPageWidth = 252.0;
        private bool _isDraggingSlider = false;
        private Point _dragStartPos;

        private static BitmapSource? _cachedFolderIcon;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource?> _cachedMockIcons = new(StringComparer.OrdinalIgnoreCase);

        // Master item pool for Folder 1 (Panak): 60+ diverse items
        // The first 4 items are displayed in the desktop icon's 2x2 mini grid
        private static readonly (string Name, bool IsFolder, string Ext)[] MasterFolder1Pool = new[] {
            ("Documents", true, ""),
            ("Pictures", true, ""),
            ("Project Plan.docx", false, ".docx"),
            ("Budget 2026.xlsx", false, ".xlsx"),
            ("Banner_Final.png", false, ".png"),
            ("Client Brief.pdf", false, ".pdf"),
            ("Downloads", true, ""),
            ("Work", true, ""),
            ("Music", true, ""),
            ("Videos", true, ""),
            ("Code", true, ""),
            ("Archive", true, ""),
            ("ReleaseDemo.mp4", false, ".mp4"),
            ("Podcast.mp3", false, ".mp3"),
            ("AppSetup.exe", false, ".exe"),
            ("SourceBackup.zip", false, ".zip"),
            ("Backups", true, ""),
            ("Configs", true, ""),
            ("Scripts", true, ""),
            ("Assets", true, ""),
            ("Notes.txt", false, ".txt"),
            ("Settings.json", false, ".json"),
            ("README.md", false, ".md"),
            ("Database.sql", false, ".sql"),
            ("StyleGuide.pdf", false, ".pdf"),
            ("QuarterlyReport.docx", false, ".docx"),
            ("Inventory.xlsx", false, ".xlsx"),
            ("Landscape.png", false, ".png"),
            ("IntroVideo.mp4", false, ".mp4"),
            ("SoundEffect.wav", false, ".wav"),
            ("InstallHelper.msi", false, ".msi"),
            ("Resources.zip", false, ".zip"),
            ("Analytics", true, ""),
            ("Research", true, ""),
            ("Prototype", true, ""),
            ("Deployments", true, ""),
            ("AppIcon.png", false, ".png"),
            ("UserStories.docx", false, ".docx"),
            ("FinanceAudit.xlsx", false, ".xlsx"),
            ("TermsOfService.pdf", false, ".pdf"),
            ("Walkthrough.mp4", false, ".mp4"),
            ("ThemeSong.mp3", false, ".mp3"),
            ("Updater.exe", false, ".exe"),
            ("Packages.7z", false, ".7z"),
            ("SetupConfig.json", false, ".json"),
            ("Migration.sql", false, ".sql"),
            ("Main.cs", false, ".cs"),
            ("Styles.css", false, ".css"),
            ("Index.html", false, ".html"),
            ("VectorLogo.svg", false, ".svg"),
            ("DashboardMockup.png", false, ".png"),
            ("KeyMetrics.xlsx", false, ".xlsx"),
            ("SystemSpecs.docx", false, ".docx"),
            ("SecurityPolicy.pdf", false, ".pdf"),
            ("Webinar.mp4", false, ".mp4"),
            ("ReleaseArchive.zip", false, ".zip"),
            ("PatchTool.exe", false, ".exe"),
            ("Documentation", true, ""),
            ("Logs", true, ""),
            ("Temp", true, "")
        };

        // Master item pool for Folder 2 (Projects): 60+ diverse items
        // The first 4 items are displayed in the desktop icon's 2x2 mini grid
        private static readonly (string Name, bool IsFolder, string Ext)[] MasterFolder2Pool = new[] {
            ("Panak Core", true, ""),
            ("UI Redesign", true, ""),
            ("Roadmap.docx", false, ".docx"),
            ("Financials.xlsx", false, ".xlsx"),
            ("Mockup_v2.png", false, ".png"),
            ("Proposal.pdf", false, ".pdf"),
            ("Marketing", true, ""),
            ("Branding", true, ""),
            ("Components", true, ""),
            ("Stylesheets", true, ""),
            ("BuildArtifacts", true, ""),
            ("Translations", true, ""),
            ("Showreel.mp4", false, ".mp4"),
            ("Voiceover.mp3", false, ".mp3"),
            ("Installer.exe", false, ".exe"),
            ("DeployBundle.zip", false, ".zip"),
            ("Specs", true, ""),
            ("Reports", true, ""),
            ("Feedback", true, ""),
            ("Templates", true, ""),
            ("MeetingLog.txt", false, ".txt"),
            ("Config.json", false, ".json"),
            ("CHANGELOG.md", false, ".md"),
            ("Schema.sql", false, ".sql"),
            ("BrandGuidelines.pdf", false, ".pdf"),
            ("PitchDeck.pptx", false, ".pptx"),
            ("ExpenseSheet.xlsx", false, ".xlsx"),
            ("HeroBanner.png", false, ".png"),
            ("ProductDemo.mp4", false, ".mp4"),
            ("Interview.mp3", false, ".mp3"),
            ("DeployScript.ps1", false, ".ps1"),
            ("ReleasePack.zip", false, ".zip"),
            ("UserInterviews", true, ""),
            ("Benchmarks", true, ""),
            ("QA Testing", true, ""),
            ("Legal", true, ""),
            ("Wireframes.pdf", false, ".pdf"),
            ("FeatureSpec.docx", false, ".docx"),
            ("ProfitModel.xlsx", false, ".xlsx"),
            ("IconSet_v3.png", false, ".png"),
            ("PromoTrailer.mp4", false, ".mp4"),
            ("Jingle.wav", false, ".wav"),
            ("ServerSetup.exe", false, ".exe"),
            ("BuildLogs.zip", false, ".zip"),
            ("DatabaseDump.sql", false, ".sql"),
            ("PackageLock.json", false, ".json"),
            ("Frontend.ts", false, ".ts"),
            ("Components.tsx", false, ".tsx"),
            ("Contract.pdf", false, ".pdf"),
            ("DesignTokens.json", false, ".json"),
            ("SplashArt.png", false, ".png"),
            ("MarketingPlan.docx", false, ".docx"),
            ("CostAnalysis.xlsx", false, ".xlsx"),
            ("ConferenceTalk.mp4", false, ".mp4"),
            ("ReleaseNotes.md", false, ".md"),
            ("ArchiveTool.exe", false, ".exe"),
            ("Milestones", true, ""),
            ("Releases", true, ""),
            ("SharedAssets", true, ""),
            ("CustomerSupport", true, "")
        };

        public WelcomeWindow() {
            InitializeComponent();
            LoadLogo();
            LoadInitialSettings();
            ApplyLocalization();
            StartWallpaperCapture();
            RebuildPreviewItems();
            UpdateLivePreview();
            _isInitializing = false;
        }

        private void LoadLogo() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            BitmapImage? iconImage = null;
            try {
                iconImage = new BitmapImage(new Uri("pack://application:,,,/Logo.png", UriKind.Absolute));
            } catch {
                try {
                    iconImage = new BitmapImage(new Uri("pack://application:,,,/Frame 121.png", UriKind.Absolute));
                } catch {
                    string[] iconCandidates = {
                        Path.Combine(baseDir, "src", "Logo.png"),
                        Path.Combine(baseDir, "Logo.png"),
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
            }

            if (iconImage != null) {
                Icon = iconImage;
            }
        }

        private void LoadInitialSettings() {
            var s = SettingsService.Instance.Current;
            _selectedShape = string.IsNullOrEmpty(s.IconShape) ? "Squircle" : s.IconShape;
            _selectedColor = string.IsNullOrEmpty(s.ThemeColor) ? ShellHelper.GetDefaultPastelColorHex() : s.ThemeColor;
            _folderOpacity = s.FolderOpacity;
            _folderBlurEnabled = s.FolderBlurEnabled;
            _folderBlurRadius = s.FolderBlurRadius;

            _pagingMode = string.IsNullOrEmpty(s.PagingMode) ? "Slider" : s.PagingMode;
            _gridColumns = s.GridColumns > 0 ? s.GridColumns : 5;
            _gridRows = s.GridRows > 0 ? s.GridRows : 3;
            _sliderArrows = s.SliderArrowsEnabled;
            _sliderDrag = s.SliderDragEnabled;
            _focusMode = s.FocusModeEnabled;
            _focusDimming = s.FocusDimmingOpacity;
            _focusBlurEnabled = s.FocusBlurEnabled;
            _focusBlurRadius = s.FocusBlurRadius > 0 ? s.FocusBlurRadius : 28.0;

            // Step 1 controls
            if (string.Equals(_selectedShape, "Circle", StringComparison.OrdinalIgnoreCase)) ShapeCircleRadio.IsChecked = true;
            else if (string.Equals(_selectedShape, "Clover", StringComparison.OrdinalIgnoreCase)) ShapeCloverRadio.IsChecked = true;
            else if (string.Equals(_selectedShape, "Scallop", StringComparison.OrdinalIgnoreCase)) ShapeScallopRadio.IsChecked = true;
            else if (string.Equals(_selectedShape, "Arch", StringComparison.OrdinalIgnoreCase)) ShapeArchRadio.IsChecked = true;
            else ShapeSquircleRadio.IsChecked = true;

            double hue = ShellHelper.GetHueFromHex(_selectedColor);
            Step1HueSlider.Value = hue;
            Step1OpacitySlider.Value = Math.Round(_folderOpacity * 100);
            Step1OpacityValueText.Text = $"{(int)Step1OpacitySlider.Value}%";
            if (Step1BlurCheckBox != null) Step1BlurCheckBox.IsChecked = _folderBlurEnabled;
            Step1BlurRadiusSlider.Value = _folderBlurRadius;
            Step1BlurRadiusSlider.IsEnabled = _folderBlurEnabled;
            Step1RadiusValueText.Text = $"{(int)_folderBlurRadius} px";
            RebuildStep1CustomSwatchesUI();


            // Step 2 controls
            if (string.Equals(_pagingMode, "Scrollbar", StringComparison.OrdinalIgnoreCase)) Step2ScrollbarRadio.IsChecked = true;
            else Step2SliderRadio.IsChecked = true;

            Step2ColsSlider.Value = _gridColumns;
            Step2ColsValueText.Text = _gridColumns.ToString();
            Step2RowsSlider.Value = _gridRows;
            Step2RowsValueText.Text = _gridRows.ToString();

            Step2FocusCheckBox.IsChecked = _focusMode;
            Step2DimmingSlider.Value = Math.Round(_focusDimming * 100);
            Step2DimmingValueText.Text = $"{(int)Step2DimmingSlider.Value}%";
            if (Step2FocusBlurCheckBox != null) Step2FocusBlurCheckBox.IsChecked = _focusBlurEnabled;
            if (Step2FocusBlurSlider != null) {
                Step2FocusBlurSlider.Value = _focusBlurRadius;
                Step2FocusBlurSlider.IsEnabled = _focusBlurEnabled;
            }
            if (Step2FocusBlurValueText != null) Step2FocusBlurValueText.Text = $"{(int)_focusBlurRadius} px";
            Step2ArrowsCheckBox.IsChecked = _sliderArrows;
            Step2DragCheckBox.IsChecked = _sliderDrag;

            ApplyMaterialYouPalette(hue);
        }

        private void StartWallpaperCapture() {
            try {
                var bmp = ShellHelper.GetDesktopWallpaperBitmap();
                if (bmp != null) {
                    SetAllWallpaperSources(bmp);
                }
            } catch { }

            try {
                if (ShellHelper.IsLiveWallpaperEngineRunning()) {
                    _liveWallpaperTimer = new DispatcherTimer {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    _liveWallpaperTimer.Tick += (s, e) => {
                        try {
                            var liveFrame = ShellHelper.CaptureLiveWallpaper();
                            if (liveFrame != null) {
                                SetAllWallpaperSources(liveFrame);
                            }
                        } catch { }
                    };
                    _liveWallpaperTimer.Start();
                }
            } catch { }
        }

        private void SetAllWallpaperSources(ImageSource src) {
            if (Step1WallpaperImage != null) Step1WallpaperImage.Source = src;
            if (Step1BlurredWallpaperImage != null) Step1BlurredWallpaperImage.Source = src;
            if (Step1BlurredWallpaperImage2 != null) Step1BlurredWallpaperImage2.Source = src;
            if (Step2WallpaperImage != null) Step2WallpaperImage.Source = src;
            if (Step2BlurredWallpaperImage != null) Step2BlurredWallpaperImage.Source = src;
            if (Step2BlurredWallpaperImage2 != null) Step2BlurredWallpaperImage2.Source = src;
            if (Step3WallpaperImage != null) Step3WallpaperImage.Source = src;
            if (Step3BlurredWallpaperImage != null) Step3BlurredWallpaperImage.Source = src;
            if (Step3BlurredWallpaperImage2 != null) Step3BlurredWallpaperImage2.Source = src;
        }

        private void ApplyLocalization() {
            var loc = LocalizationService.Instance;

            Title = loc["WelcomeToPanak"];
            StepIndicatorText.Text = $"{loc["Step"]} {_currentStep + 1} / 3";
            Step1TitleText.Text = loc["Step1Title"] ?? "Գույն, ձեւ եւ թափանցիկություն";
            Step1SubtitleText.Text = loc["Step1Subtitle"];
            Step1ShapeLabel.Text = loc["IconShape"];
            ShapeCircleRadio.ToolTip = loc["Circle"];
            ShapeSquircleRadio.ToolTip = loc["Squircle"];
            ShapeCloverRadio.ToolTip = loc["Clover"];
            ShapeScallopRadio.ToolTip = loc["Scallop"];
            ShapeArchRadio.ToolTip = loc["Arch"];
            Step1ColorLabel.Text = loc["DefaultFolderColor"];
            Step1OpacityLabel.Text = loc["GeneralFolderOpacity"];
            Step1BlurLabel.Text = loc["FolderBlur"];
            if (Step1BlurWarningBadge != null) Step1BlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            if (Step1RadiusLabel != null) Step1RadiusLabel.Text = loc["FolderBlurRadius"];
            Step1LivePreviewBadge.Text = loc["LivePreview"];

            string countFormatted = string.Format(loc["ItemsCountBadge"] ?? "{0} նիշք", 24);
            if (Step1PreviewCountText != null) Step1PreviewCountText.Text = countFormatted;
            if (Step2PreviewCountText != null) Step2PreviewCountText.Text = countFormatted;
            if (Step3PreviewCountText != null) Step3PreviewCountText.Text = countFormatted;
            if (Step1PreviewCountText2 != null) Step1PreviewCountText2.Text = countFormatted;
            if (Step2PreviewCountText2 != null) Step2PreviewCountText2.Text = countFormatted;
            if (Step3PreviewCountText2 != null) Step3PreviewCountText2.Text = countFormatted;

            Step2TitleText.Text = loc["Step2Title"] ?? "Դիրք եւ վարքագիծ";
            Step2SubtitleText.Text = loc["Step2Subtitle"];
            Step2PagingModeLabel.Text = loc["PagingMode"];
            Step2SliderRadio.Content = loc["PagingModeSlider"];
            Step2ScrollbarRadio.Content = loc["PagingModeScrollbar"];
            Step2ColsLabel.Text = loc["ItemsPerRow"];
            Step2RowsLabel.Text = loc["GridRows"];
            Step2FocusModeLabel.Text = loc["FocusMode"];
            Step2DimmingLabel.Text = loc["FocusDimming"];
            if (Step2FocusBlurLabel != null) Step2FocusBlurLabel.Text = loc["FocusBlur"];
            if (Step2FocusBlurWarningBadge != null) Step2FocusBlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            if (Step2FocusBlurRadiusLabel != null) Step2FocusBlurRadiusLabel.Text = loc["FocusBlurRadius"];
            Step2ArrowsLabel.Text = loc["SliderArrows"];
            Step2DragLabel.Text = loc["SliderDrag"];

            Step3TitleText.Text = loc["WelcomeToPanak"];
            Step3SubtitleText.Text = loc["WelcomeSubtitle"];
            Feature1Text.Text = loc["WelcomeFeature1"];
            Feature2Text.Text = loc["WelcomeFeature2"];
            Feature3Text.Text = loc["WelcomeFeature3"];
            SettingsBtn.Content = "⚙️ " + loc["Settings"];
            KofiBtn.Content = loc["DonateOnKofi"];
            if (CrowdinBtn != null) CrowdinBtn.ToolTip = loc["TranslateOnCrowdin"];

            BackBtn.Content = loc["Back"];
            SkipBtn.Content = loc["Skip"];
            UpdateStepButtons();
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

                Resources["M3SurfaceBrush"] = new SolidColorBrush(toWpf(surfaceColor));
                Resources["M3SurfaceContainerBrush"] = new SolidColorBrush(toWpf(containerColor));
                Resources["M3CardBrush"] = new SolidColorBrush(toWpf(cardColor));
                Resources["M3PrimaryBrush"] = new SolidColorBrush(toWpf(primaryColor));
                Resources["M3OnPrimaryBrush"] = new SolidColorBrush(Colors.White);
                Resources["M3OnSurfaceBrush"] = new SolidColorBrush(toWpf(onSurfaceColor));
                Resources["M3OnSurfaceVariantBrush"] = new SolidColorBrush(toWpf(onSurfaceVariantColor));
                Resources["M3TrackInactiveBrush"] = new SolidColorBrush(toWpf(trackInactiveColor));

                Color selColor;
                try {
                    selColor = (Color)ColorConverter.ConvertFromString(_selectedColor);
                } catch {
                    selColor = toWpf(primaryColor);
                }
                Resources["SettingsHueThumbBrush"] = new SolidColorBrush(selColor);

                var (cContainer, cTriangle, cCloud, cShield, cEllipse) = ShellHelper.GetLogoDynamicColors(hue, false);
                Resources["LogoContainerBrush"] = new SolidColorBrush(toWpf(cContainer));
                Resources["LogoTriangleBrush"] = new SolidColorBrush(toWpf(cTriangle));
                Resources["LogoCloudBrush"] = new SolidColorBrush(toWpf(cCloud));
                Resources["LogoShieldBrush"] = new SolidColorBrush(toWpf(cShield));
                Resources["LogoEllipseBrush"] = new SolidColorBrush(toWpf(cEllipse));
                Resources["LogoTextBrush"] = new SolidColorBrush(toWpf(cTriangle));

                if (Step1ColorIndicator != null) {
                    Step1ColorIndicator.Background = new SolidColorBrush(selColor);
                }
            } catch { }
        }

        private void RebuildPreviewItems() {
            int actualCols = Math.Clamp(_gridColumns, 1, 8);
            int actualRows = Math.Clamp(_gridRows, 1, 5);
            int itemsPerPage = Math.Max(1, actualCols * actualRows);

            // Last page (Page 2) is partially filled; Pages 0 and 1 are 100% full
            int lastPageCount = Math.Max(2, (int)Math.Round(itemsPerPage * 0.65));
            if (lastPageCount >= itemsPerPage) lastPageCount = Math.Max(1, itemsPerPage - 1);

            // Dynamically construct 3 slider pages from master pools
            var f1Page0 = MasterFolder1Pool.Take(itemsPerPage).ToArray();
            var f1Page1 = MasterFolder1Pool.Skip(itemsPerPage).Take(itemsPerPage).ToArray();
            var f1Page2 = MasterFolder1Pool.Skip(itemsPerPage * 2).Take(lastPageCount).ToArray();
            var folder1Pages = new[] { f1Page0, f1Page1, f1Page2 };
            int totalFolder1Items = f1Page0.Length + f1Page1.Length + f1Page2.Length;

            var f2Page0 = MasterFolder2Pool.Take(itemsPerPage).ToArray();
            var f2Page1 = MasterFolder2Pool.Skip(itemsPerPage).Take(itemsPerPage).ToArray();
            var f2Page2 = MasterFolder2Pool.Skip(itemsPerPage * 2).Take(lastPageCount).ToArray();
            var folder2Pages = new[] { f2Page0, f2Page1, f2Page2 };
            int totalFolder2Items = f2Page0.Length + f2Page1.Length + f2Page2.Length;

            _previewPageWidth = 252.0;
            _totalPreviewPages = 3;

            if (_currentPreviewPage >= _totalPreviewPages) _currentPreviewPage = _totalPreviewPages - 1;
            if (_currentPreviewPage < 0) _currentPreviewPage = 0;

            bool isSlider = string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase);
            bool showPager = isSlider && _totalPreviewPages > 1;

            void ConfigureViews(Grid? sliderVp, ScrollViewer? scrollVw, Grid? pagerGrid, Button? prevBtn, Button? nextBtn) {
                if (sliderVp != null) sliderVp.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
                if (scrollVw != null) scrollVw.Visibility = isSlider ? Visibility.Collapsed : Visibility.Visible;
                if (pagerGrid != null) pagerGrid.Visibility = showPager ? Visibility.Visible : Visibility.Collapsed;
                if (prevBtn != null) prevBtn.Visibility = (showPager && _sliderArrows) ? Visibility.Visible : Visibility.Collapsed;
                if (nextBtn != null) nextBtn.Visibility = (showPager && _sliderArrows) ? Visibility.Visible : Visibility.Collapsed;
            }

            ConfigureViews(Step1SliderViewport, Step1ScrollViewer, Step1FooterPagerGrid, Step1PrevBtn, Step1NextBtn);
            ConfigureViews(Step1SliderViewport2, null, Step1FooterPagerGrid2, null, null);
            ConfigureViews(Step2SliderViewport, Step2ScrollViewer, Step2FooterPagerGrid, Step2PrevBtn, Step2NextBtn);
            ConfigureViews(Step2SliderViewport2, null, Step2FooterPagerGrid2, null, null);
            ConfigureViews(Step3SliderViewport, Step3ScrollViewer, Step3FooterPagerGrid, Step3PrevBtn, Step3NextBtn);
            ConfigureViews(Step3SliderViewport2, null, Step3FooterPagerGrid2, null, null);

            // Folder 1 Slider Pages (Panak)
            BuildSliderPages(Step1SliderPagesPanel, actualCols, actualRows, folder1Pages);
            BuildSliderPages(Step2SliderPagesPanel, actualCols, actualRows, folder1Pages);
            BuildSliderPages(Step3SliderPagesPanel, actualCols, actualRows, folder1Pages);

            // Folder 2 Slider Pages (Projects)
            BuildSliderPages(Step1SliderPagesPanel2, actualCols, actualRows, folder2Pages);
            BuildSliderPages(Step2SliderPagesPanel2, actualCols, actualRows, folder2Pages);
            BuildSliderPages(Step3SliderPagesPanel2, actualCols, actualRows, folder2Pages);

            // Scroll Grids
            BuildScrollGrid(Step1ScrollGrid, actualCols, folder1Pages);
            BuildScrollGrid(Step2ScrollGrid, actualCols, folder1Pages);
            BuildScrollGrid(Step3ScrollGrid, actualCols, folder1Pages);

            // Desktop folder icons: 2x2 mini grid showing the EXACT first 4 items of each popup
            UpdateDesktopIconMiniGrid(Step1DesktopIcon1MiniGrid, f1Page0);
            UpdateDesktopIconMiniGrid(Step2DesktopIcon1MiniGrid, f1Page0);
            UpdateDesktopIconMiniGrid(Step3DesktopIcon1MiniGrid, f1Page0);

            UpdateDesktopIconMiniGrid(Step1DesktopIcon2MiniGrid, f2Page0);
            UpdateDesktopIconMiniGrid(Step2DesktopIcon2MiniGrid, f2Page0);
            UpdateDesktopIconMiniGrid(Step3DesktopIcon2MiniGrid, f2Page0);

            UpdateAllPreviewDots();
            AnimatePreviewPage(false);

            var loc = LocalizationService.Instance;
            string countFormatted1 = string.Format(loc["ItemsCountBadge"] ?? "{0} նիշք", totalFolder1Items);
            string countFormatted2 = string.Format(loc["ItemsCountBadge"] ?? "{0} նիշք", totalFolder2Items);
            if (Step1PreviewCountText != null) Step1PreviewCountText.Text = countFormatted1;
            if (Step2PreviewCountText != null) Step2PreviewCountText.Text = countFormatted1;
            if (Step3PreviewCountText != null) Step3PreviewCountText.Text = countFormatted1;
            if (Step1PreviewCountText2 != null) Step1PreviewCountText2.Text = countFormatted2;
            if (Step2PreviewCountText2 != null) Step2PreviewCountText2.Text = countFormatted2;
            if (Step3PreviewCountText2 != null) Step3PreviewCountText2.Text = countFormatted2;
        }

        private static ImageSource? GetMockIconSource(bool isFolder, string ext) {
            try {
                if (isFolder) {
                    if (_cachedFolderIcon != null) return _cachedFolderIcon;
                    _cachedFolderIcon = ShellHelper.GetFolderIconSource(isLarge: true);
                    return _cachedFolderIcon;
                }

                if (!string.IsNullOrEmpty(ext)) {
                    if (_cachedMockIcons.TryGetValue(ext, out var cached)) return cached;
                    var src = ShellHelper.GetFileIconSource("sample" + ext, isLarge: true);
                    if (src != null) {
                        _cachedMockIcons[ext] = src;
                        return src;
                    }
                }
            } catch { }
            return null;
        }

        private static FrameworkElement CreateCrispFileBadge(string ext) {
            string normExt = (ext ?? "").TrimStart('.').ToUpperInvariant();
            string label = string.IsNullOrEmpty(normExt) ? "FILE" : (normExt.Length > 4 ? normExt.Substring(0, 4) : normExt);

            Color bg;
            switch (normExt) {
                case "DOCX":
                case "DOC":
                    bg = Color.FromRgb(43, 87, 154);
                    break;
                case "XLSX":
                case "XLS":
                    bg = Color.FromRgb(33, 115, 70);
                    break;
                case "PDF":
                    bg = Color.FromRgb(211, 47, 47);
                    break;
                case "PNG":
                case "JPG":
                case "JPEG":
                case "SVG":
                case "GIF":
                    bg = Color.FromRgb(2, 136, 209);
                    break;
                case "MP4":
                case "MOV":
                case "AVI":
                    bg = Color.FromRgb(123, 31, 162);
                    break;
                case "MP3":
                case "WAV":
                case "FLAC":
                    bg = Color.FromRgb(230, 81, 0);
                    break;
                case "ZIP":
                case "RAR":
                case "7Z":
                    bg = Color.FromRgb(245, 127, 23);
                    break;
                case "EXE":
                case "MSI":
                    bg = Color.FromRgb(57, 73, 171);
                    break;
                case "CS":
                case "JS":
                case "TS":
                case "JSON":
                case "SQL":
                    bg = Color.FromRgb(0, 137, 123);
                    break;
                default:
                    bg = Color.FromRgb(84, 110, 122);
                    break;
            }

            var badge = new Border {
                Width = 26,
                Height = 30,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(bg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect {
                    BlurRadius = 3,
                    ShadowDepth = 1,
                    Opacity = 0.25,
                    Color = Colors.Black
                }
            };

            var grid = new Grid();
            var fold = new Border {
                Width = 7,
                Height = 7,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                CornerRadius = new CornerRadius(0, 4, 0, 4)
            };
            grid.Children.Add(fold);

            var txt = new TextBlock {
                Text = label,
                FontSize = label.Length > 3 ? 6.5 : 7.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1, 0, 1, 3)
            };
            grid.Children.Add(txt);

            badge.Child = grid;
            return badge;
        }

        private static FrameworkElement CreateVectorFolderIcon() {
            return new System.Windows.Shapes.Path {
                Data = Geometry.Parse("M 2,4 L 10,4 L 12,6 L 26,6 C 27.1,6 28,6.9 28,8 L 28,22 C 28,23.1 27.1,24 26,24 L 2,24 C 0.9,24 0,23.1 0,22 L 0,6 C 0,4.9 0.9,4 2,4 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(255, 179, 0)),
                Stretch = Stretch.Uniform,
                Width = 30,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect {
                    BlurRadius = 3,
                    ShadowDepth = 1,
                    Opacity = 0.25,
                    Color = Colors.Black
                }
            };
        }

        private static FrameworkElement BuildMockItemIconVisual(bool isFolder, string ext) {
            if (isFolder) {
                var src = GetMockIconSource(true, "");
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 32,
                        Height = 32,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return CreateVectorFolderIcon();
            } else {
                var src = GetMockIconSource(false, ext);
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 28,
                        Height = 28,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return CreateCrispFileBadge(ext);
            }
        }

        private static FrameworkElement BuildMiniMockItemVisual(bool isFolder, string ext) {
            if (isFolder) {
                var src = GetMockIconSource(true, "");
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 13,
                        Height = 13,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return new System.Windows.Shapes.Path {
                    Data = Geometry.Parse("M 2,4 L 10,4 L 12,6 L 26,6 C 27.1,6 28,6.9 28,8 L 28,22 C 28,23.1 27.1,24 26,24 L 2,24 C 0.9,24 0,23.1 0,22 L 0,6 C 0,4.9 0.9,4 2,4 Z"),
                    Fill = new SolidColorBrush(Color.FromRgb(255, 179, 0)),
                    Stretch = Stretch.Uniform,
                    Width = 12,
                    Height = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            } else {
                var src = GetMockIconSource(false, ext);
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 13,
                        Height = 13,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return CreateMiniCrispFileBadge(ext);
            }
        }

        private static FrameworkElement CreateMiniCrispFileBadge(string ext) {
            string normExt = (ext ?? "").TrimStart('.').ToUpperInvariant();
            string label = string.IsNullOrEmpty(normExt) ? "F" : (normExt.Length > 3 ? normExt.Substring(0, 3) : normExt);
            Color bg;
            switch (normExt) {
                case "DOCX": case "DOC": bg = Color.FromRgb(43, 87, 154); break;
                case "XLSX": case "XLS": bg = Color.FromRgb(33, 115, 70); break;
                case "PDF": bg = Color.FromRgb(211, 47, 47); break;
                case "PNG": case "JPG": case "JPEG": case "SVG": bg = Color.FromRgb(2, 136, 209); break;
                case "MP4": case "MOV": case "AVI": bg = Color.FromRgb(123, 31, 162); break;
                case "MP3": case "WAV": bg = Color.FromRgb(230, 81, 0); break;
                case "ZIP": case "RAR": bg = Color.FromRgb(245, 127, 23); break;
                case "EXE": case "MSI": bg = Color.FromRgb(57, 73, 171); break;
                default: bg = Color.FromRgb(84, 110, 122); break;
            }
            var border = new Border {
                Width = 12,
                Height = 13,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(bg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var text = new TextBlock {
                Text = label,
                FontSize = 6,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = text;
            return border;
        }

        private static void UpdateDesktopIconMiniGrid(System.Windows.Controls.Primitives.UniformGrid? grid, (string Name, bool IsFolder, string Ext)[] page0Items) {
            if (grid == null) return;
            grid.Children.Clear();
            var firstFour = page0Items.Take(4).ToList();
            foreach (var item in firstFour) {
                var cell = new Border {
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(0x35, 255, 255, 255)),
                    Margin = new Thickness(1.5),
                    Child = BuildMiniMockItemVisual(item.IsFolder, item.Ext)
                };
                grid.Children.Add(cell);
            }
            while (grid.Children.Count < 4) {
                grid.Children.Add(new Border {
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255)),
                    Margin = new Thickness(1.5)
                });
            }
        }

        private FrameworkElement CreateMockItemView(string name, bool isFolder, string ext, double cellW, double cellH) {
            var border = new Border {
                Width = cellW,
                Height = cellH,
                Margin = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Padding = new Thickness(2, 3, 2, 2)
            };
            border.MouseEnter += (s, e) => border.Background = Resources["PopupItemHoverBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;

            var sp = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = Math.Max(20, cellW - 4)
            };

            var iconContainer = new Grid {
                Width = Math.Clamp(cellW * 0.55, 24, 32),
                Height = Math.Clamp(cellH * 0.45, 24, 32),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconContainer.Children.Add(BuildMockItemIconVisual(isFolder, ext));

            double fontSize = Math.Clamp(cellW * 0.16, 8.5, 9.8);
            var nameText = new TextBlock {
                Text = name,
                FontSize = fontSize,
                Foreground = Resources["PopupTextBrush"] as Brush ?? Brushes.Black,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 24,
                Margin = new Thickness(0, 2, 0, 0),
                ToolTip = name
            };

            sp.Children.Add(iconContainer);
            sp.Children.Add(nameText);
            border.Child = sp;
            return border;
        }

        private void BuildSliderPages(StackPanel? panel, int cols, int rows, (string Name, bool IsFolder, string Ext)[][] folderPages) {
            if (panel == null) return;
            panel.Children.Clear();
            panel.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Width = _totalPreviewPages * _previewPageWidth;
            panel.Height = 166.0;

            double cellW = _previewPageWidth / Math.Max(1, cols);
            double cellH = 166.0 / Math.Max(1, rows);
            int itemsPerPage = Math.Max(1, cols * rows);

            for (int p = 0; p < _totalPreviewPages; p++) {
                var grid = new UniformGrid {
                    Columns = cols,
                    Rows = rows,
                    Width = _previewPageWidth,
                    Height = 166.0,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var pageItems = folderPages[p % folderPages.Length];
                int count = Math.Min(itemsPerPage, pageItems.Length);
                for (int i = 0; i < count; i++) {
                    var item = pageItems[i];
                    grid.Children.Add(CreateMockItemView(item.Name, item.IsFolder, item.Ext, cellW, cellH));
                }

                panel.Children.Add(grid);
            }
        }

        private void BuildScrollGrid(UniformGrid? grid, int cols, (string Name, bool IsFolder, string Ext)[][] folderPages) {
            if (grid == null) return;
            grid.Children.Clear();
            grid.Columns = cols;
            grid.Width = _previewPageWidth;

            double cellW = _previewPageWidth / Math.Max(1, cols);
            double cellH = 76.0;

            foreach (var page in folderPages) {
                foreach (var item in page) {
                    grid.Children.Add(CreateMockItemView(item.Name, item.IsFolder, item.Ext, cellW, cellH));
                }
            }
        }

        private void UpdatePreviewDots(StackPanel? panel) {
            if (panel == null) return;
            panel.Children.Clear();

            if (_totalPreviewPages <= 1) return;

            var activeBrush = Resources["M3PrimaryBrush"] as Brush ?? Brushes.DarkKhaki;
            var inactiveBrush = Resources["M3TrackInactiveBrush"] as Brush ?? Brushes.LightGray;

            for (int i = 0; i < _totalPreviewPages; i++) {
                int pageIndex = i;
                bool isActive = (i == _currentPreviewPage);
                var dot = new Border {
                    Width = isActive ? 18 : 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = isActive ? activeBrush : inactiveBrush,
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    Cursor = Cursors.Hand
                };
                dot.MouseDown += (s, e) => {
                    _currentPreviewPage = pageIndex;
                    AnimatePreviewPage(true);
                    UpdateAllPreviewDots();
                };
                panel.Children.Add(dot);
            }
        }

        private void UpdateAllPreviewDots() {
            UpdatePreviewDots(Step1DotsPanel);
            UpdatePreviewDots(Step1DotsPanel2);
            UpdatePreviewDots(Step2DotsPanel);
            UpdatePreviewDots(Step2DotsPanel2);
            UpdatePreviewDots(Step3DotsPanel);
            UpdatePreviewDots(Step3DotsPanel2);
        }

        private void SetAllPagesTranslateX(double x) {
            if (Step1PagesTranslate != null) { Step1PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null); Step1PagesTranslate.X = x; }
            if (Step1PagesTranslate2 != null) { Step1PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, null); Step1PagesTranslate2.X = x; }
            if (Step2PagesTranslate != null) { Step2PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null); Step2PagesTranslate.X = x; }
            if (Step2PagesTranslate2 != null) { Step2PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, null); Step2PagesTranslate2.X = x; }
            if (Step3PagesTranslate != null) { Step3PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null); Step3PagesTranslate.X = x; }
            if (Step3PagesTranslate2 != null) { Step3PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, null); Step3PagesTranslate2.X = x; }
        }

        private void AnimatePreviewPage(bool animate) {
            double targetX = -_currentPreviewPage * _previewPageWidth;
            if (animate) {
                var anim = new DoubleAnimation {
                    To = targetX,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                if (Step1PagesTranslate != null) Step1PagesTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step1PagesTranslate2 != null) Step1PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step2PagesTranslate != null) Step2PagesTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step2PagesTranslate2 != null) Step2PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step3PagesTranslate != null) Step3PagesTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step3PagesTranslate2 != null) Step3PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, anim);
            } else {
                SetAllPagesTranslateX(targetX);
            }
        }

        private void PreviewSlider_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase)) {
                if (e.Delta < 0 && _currentPreviewPage < _totalPreviewPages - 1) {
                    _currentPreviewPage++;
                    AnimatePreviewPage(true);
                    UpdateAllPreviewDots();
                    e.Handled = true;
                } else if (e.Delta > 0 && _currentPreviewPage > 0) {
                    _currentPreviewPage--;
                    AnimatePreviewPage(true);
                    UpdateAllPreviewDots();
                    e.Handled = true;
                }
            }
        }

        private void PreviewSlider_MouseDown(object sender, MouseButtonEventArgs e) {
            if (_sliderDrag && e.LeftButton == MouseButtonState.Pressed) {
                _isDraggingSlider = true;
                _dragStartPos = e.GetPosition(this);
                if (sender is UIElement el) el.CaptureMouse();
            }
        }

        private void PreviewSlider_MouseMove(object sender, MouseEventArgs e) {
            if (_isDraggingSlider && _sliderDrag && e.LeftButton == MouseButtonState.Pressed) {
                var curPos = e.GetPosition(this);
                double deltaX = curPos.X - _dragStartPos.X;
                double baseX = -_currentPreviewPage * _previewPageWidth;
                SetAllPagesTranslateX(baseX + deltaX);
            }
        }

        private void PreviewSlider_MouseUp(object sender, MouseButtonEventArgs e) {
            if (_isDraggingSlider) {
                _isDraggingSlider = false;
                if (sender is UIElement el) el.ReleaseMouseCapture();
                var endPos = e.GetPosition(this);
                double deltaX = endPos.X - _dragStartPos.X;
                if (deltaX < -30 && _currentPreviewPage < _totalPreviewPages - 1) {
                    _currentPreviewPage++;
                } else if (deltaX > 30 && _currentPreviewPage > 0) {
                    _currentPreviewPage--;
                }
                AnimatePreviewPage(true);
                UpdateAllPreviewDots();
            }
        }

        private void Step1PrevBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentPreviewPage > 0) {
                _currentPreviewPage--;
                AnimatePreviewPage(true);
                UpdateAllPreviewDots();
            }
        }

        private void Step1NextBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentPreviewPage < _totalPreviewPages - 1) {
                _currentPreviewPage++;
                AnimatePreviewPage(true);
                UpdateAllPreviewDots();
            }
        }

        private void TogglePopup1() {
            _isPopup1Open = !_isPopup1Open;
            if (_isPopup1Open && _isPopup2Open) {
                _isPopup2Open = false;
                AnimatePopupState(
                    false,
                    new[] { Step1PopupContainer2, Step2PopupContainer2, Step3PopupContainer2 },
                    new[] { Step1PopupScale2, Step2PopupScale2, Step3PopupScale2 }
                );
            }
            AnimatePopupState(
                _isPopup1Open,
                new[] { Step1PopupContainer, Step2PopupContainer, Step3PopupContainer },
                new[] { Step1PopupScale, Step2PopupScale, Step3PopupScale }
            );
        }

        private void TogglePopup2() {
            _isPopup2Open = !_isPopup2Open;
            if (_isPopup2Open && _isPopup1Open) {
                _isPopup1Open = false;
                AnimatePopupState(
                    false,
                    new[] { Step1PopupContainer, Step2PopupContainer, Step3PopupContainer },
                    new[] { Step1PopupScale, Step2PopupScale, Step3PopupScale }
                );
            }
            AnimatePopupState(
                _isPopup2Open,
                new[] { Step1PopupContainer2, Step2PopupContainer2, Step3PopupContainer2 },
                new[] { Step1PopupScale2, Step2PopupScale2, Step3PopupScale2 }
            );
        }

        private void ClosePopup1() {
            if (_isPopup1Open) TogglePopup1();
        }

        private void ClosePopup2() {
            if (_isPopup2Open) TogglePopup2();
        }

        private void AnimatePopupState(bool open, Grid?[] containers, ScaleTransform?[] scales) {
            var origin = new Point(0.0, 1.0); // Animate from bottom-left folder icon
            foreach (var container in containers) {
                if (container != null) container.RenderTransformOrigin = origin;
            }

            if (!open) {
                var scaleAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180));
                scaleAnim.Completed += (s, e) => {
                    foreach (var container in containers) {
                        if (container != null) container.Visibility = Visibility.Collapsed;
                    }
                };
                for (int i = 0; i < scales.Length; i++) {
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                foreach (var container in containers) {
                    container?.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                }
            } else {
                foreach (var container in containers) {
                    if (container != null) {
                        container.Visibility = Visibility.Visible;
                        container.Opacity = 0.0;
                    }
                }
                var scaleAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                for (int i = 0; i < scales.Length; i++) {
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                foreach (var container in containers) {
                    container?.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                }
            }
        }

        private void DesktopIcon1_MouseDown(object sender, MouseButtonEventArgs e) {
            TogglePopup1();
        }

        private void DesktopIcon_MouseDown(object sender, MouseButtonEventArgs e) {
            TogglePopup1();
        }

        private void DesktopIcon2_MouseDown(object sender, MouseButtonEventArgs e) {
            TogglePopup2();
        }

        private void PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void Step1PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void Step1PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void Step2PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void Step2PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void Step3PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void Step3PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void PreviewBorder_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (sender is Border b && b.Clip is RectangleGeometry rectGeom) {
                rectGeom.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
        }

        private void UpdateLivePreview() {
            try {
                Color baseCol;
                try {
                    baseCol = (Color)ColorConverter.ConvertFromString(_selectedColor);
                } catch {
                    baseCol = Color.FromRgb(0xFC, 0xDD, 0xC5);
                }

                byte alpha = (byte)(Math.Clamp(_folderOpacity, 0.2, 1.0) * 255);
                var folderBrush = new SolidColorBrush(Color.FromArgb(alpha, baseCol.R, baseCol.G, baseCol.B));

                // Determine luminance for dynamic text/button contrast in the popup card (matching FolderPopupWindow)
                double luminance = (0.299 * baseCol.R + 0.587 * baseCol.G + 0.114 * baseCol.B) / 255.0;
                bool isLight = luminance > 0.55;

                if (isLight) {
                    Resources["PopupTextBrush"] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    Resources["PopupSubtextBrush"] = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                    Resources["PopupHeaderBtnFg"] = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
                    Resources["PopupItemBadgeFg"] = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
                } else {
                    Resources["PopupTextBrush"] = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                    Resources["PopupSubtextBrush"] = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupHeaderBtnFg"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupItemBadgeFg"] = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
                }

                // Step 1 - Folder 1
                if (Step1FolderCardBorder != null) {
                    Step1FolderCardBorder.Background = folderBrush;
                    if (Step1FolderBlurFx != null) Step1FolderBlurFx.Radius = _folderBlurRadius;
                    if (Step1BlurUnderlay != null) Step1BlurUnderlay.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    if (Step1DesktopIconShapePath != null) {
                        Step1DesktopIconShapePath.Fill = folderBrush;
                        ApplyShapeToDesktopIconPlate(Step1DesktopIconShapePath, null, _selectedShape);
                    }
                }
                // Step 1 - Folder 2
                if (Step1FolderCardBorder2 != null) {
                    Step1FolderCardBorder2.Background = folderBrush;
                    if (Step1FolderBlurFx2 != null) Step1FolderBlurFx2.Radius = _folderBlurRadius;
                    if (Step1BlurUnderlay2 != null) Step1BlurUnderlay2.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    if (Step1DesktopIcon2ShapePath != null) {
                        Step1DesktopIcon2ShapePath.Fill = folderBrush;
                        ApplyShapeToDesktopIconPlate(Step1DesktopIcon2ShapePath, null, _selectedShape);
                    }
                }

                // Step 2 - Folder 1
                if (Step2FolderCardBorder != null) {
                    Step2FolderCardBorder.Background = folderBrush;
                    if (Step2FolderBlurFx != null) Step2FolderBlurFx.Radius = _folderBlurRadius;
                    if (Step2BlurUnderlay != null) Step2BlurUnderlay.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    if (Step2DesktopIconShapePath != null) {
                        Step2DesktopIconShapePath.Fill = folderBrush;
                        ApplyShapeToDesktopIconPlate(Step2DesktopIconShapePath, null, _selectedShape);
                    }
                }
                // Step 2 - Folder 2
                if (Step2FolderCardBorder2 != null) {
                    Step2FolderCardBorder2.Background = folderBrush;
                    if (Step2FolderBlurFx2 != null) Step2FolderBlurFx2.Radius = _folderBlurRadius;
                    if (Step2BlurUnderlay2 != null) Step2BlurUnderlay2.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    if (Step2DesktopIcon2ShapePath != null) {
                        Step2DesktopIcon2ShapePath.Fill = folderBrush;
                        ApplyShapeToDesktopIconPlate(Step2DesktopIcon2ShapePath, null, _selectedShape);
                    }
                }
                // Step 2 Focus Overlay & Blur
                if (Step2FocusDimOverlay != null) Step2FocusDimOverlay.Opacity = _focusMode ? _focusDimming : 0.0;
                if (Step2FocusWallpaperBlurFx != null) Step2FocusWallpaperBlurFx.Radius = (_focusMode && _focusBlurEnabled) ? _focusBlurRadius : 0.0;

                // Step 3 - Folder 1
                if (Step3FolderCardBorder != null) {
                    Step3FolderCardBorder.Background = folderBrush;
                    if (Step3FolderBlurFx != null) Step3FolderBlurFx.Radius = _folderBlurRadius;
                    if (Step3BlurUnderlay != null) Step3BlurUnderlay.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    if (Step3DesktopIconShapePath != null) {
                        Step3DesktopIconShapePath.Fill = folderBrush;
                        ApplyShapeToDesktopIconPlate(Step3DesktopIconShapePath, null, _selectedShape);
                    }
                }
                // Step 3 - Folder 2
                if (Step3FolderCardBorder2 != null) {
                    Step3FolderCardBorder2.Background = folderBrush;
                    if (Step3FolderBlurFx2 != null) Step3FolderBlurFx2.Radius = _folderBlurRadius;
                    if (Step3BlurUnderlay2 != null) Step3BlurUnderlay2.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    if (Step3DesktopIcon2ShapePath != null) {
                        Step3DesktopIcon2ShapePath.Fill = folderBrush;
                        ApplyShapeToDesktopIconPlate(Step3DesktopIcon2ShapePath, null, _selectedShape);
                    }
                }
            } catch { }
        }

        private void ApplyShapeToPreviewBadge(System.Windows.Shapes.Path? path, Border? badge, string shape) {
            if (path == null) return;
            string geomKey = "GeomSquircle";
            if (string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomCircle";
            else if (string.Equals(shape, "Clover", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomClover";
            else if (string.Equals(shape, "Scallop", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomScallop";
            else if (string.Equals(shape, "Arch", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomArch";

            if (Resources.Contains(geomKey) && Resources[geomKey] is Geometry g) {
                path.Data = g;
            }
        }

        private void ApplyShapeToDesktopIconPlate(System.Windows.Shapes.Path? path, Border? plate, string shape) {
            if (path == null) return;
            string geomKey = "GeomSquircle";
            if (string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomCircle";
            else if (string.Equals(shape, "Clover", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomClover";
            else if (string.Equals(shape, "Scallop", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomScallop";
            else if (string.Equals(shape, "Arch", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomArch";

            if (Resources.Contains(geomKey) && Resources[geomKey] is Geometry g) {
                path.Data = g;
            }
        }

        private void GoToStep(int targetStep, bool animate = true) {
            if (targetStep < 0 || targetStep > 2 || targetStep == _currentStep) return;

            Grid currentGrid = _currentStep switch {
                0 => Step1Grid,
                1 => Step2Grid,
                _ => Step3Grid
            };

            TranslateTransform currentTranslate = _currentStep switch {
                0 => Step1Translate,
                1 => Step2Translate,
                _ => Step3Translate
            };

            Grid targetGrid = targetStep switch {
                0 => Step1Grid,
                1 => Step2Grid,
                _ => Step3Grid
            };

            TranslateTransform targetTranslate = targetStep switch {
                0 => Step1Translate,
                1 => Step2Translate,
                _ => Step3Translate
            };

            int oldStep = _currentStep;
            _currentStep = targetStep;

            if (animate) {
                bool movingForward = targetStep > oldStep;
                double currentExitX = movingForward ? -320 : 320;
                double targetStartX = movingForward ? 320 : -320;

                var currentAnim = new DoubleAnimation(0, currentExitX, TimeSpan.FromMilliseconds(260)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                var currentFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));
                currentAnim.Completed += (s, e) => {
                    currentGrid.Visibility = Visibility.Collapsed;
                    currentTranslate.X = 0;
                };
                currentTranslate.BeginAnimation(TranslateTransform.XProperty, currentAnim);
                currentGrid.BeginAnimation(UIElement.OpacityProperty, currentFade);

                targetGrid.Visibility = Visibility.Visible;
                targetGrid.Opacity = 0.0;
                targetTranslate.X = targetStartX;
                var targetAnim = new DoubleAnimation(targetStartX, 0, TimeSpan.FromMilliseconds(280)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var targetFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240));
                targetTranslate.BeginAnimation(TranslateTransform.XProperty, targetAnim);
                targetGrid.BeginAnimation(UIElement.OpacityProperty, targetFade);
            } else {
                currentGrid.Visibility = Visibility.Collapsed;
                targetGrid.Visibility = Visibility.Visible;
                targetGrid.Opacity = 1.0;
                targetTranslate.X = 0;
            }

            UpdateStepDots();
            UpdateStepButtons();
        }

        private void UpdateStepDots() {
            var activeBrush = Resources["M3PrimaryBrush"] as Brush ?? Brushes.DarkKhaki;
            var inactiveBrush = Resources["M3TrackInactiveBrush"] as Brush ?? Brushes.LightGray;

            Border[] dots = { StepDot0, StepDot1, StepDot2 };
            for (int i = 0; i < dots.Length; i++) {
                if (dots[i] == null) continue;
                bool isActive = (i == _currentStep);
                dots[i].Background = isActive ? activeBrush : inactiveBrush;

                var anim = new DoubleAnimation {
                    To = isActive ? 26.0 : 8.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                dots[i].BeginAnimation(FrameworkElement.WidthProperty, anim);
            }

            var loc = LocalizationService.Instance;
            StepIndicatorText.Text = $"{loc["Step"]} {_currentStep + 1} / 3";
        }

        private void UpdateStepButtons() {
            var loc = LocalizationService.Instance;
            BackBtn.Visibility = (_currentStep > 0) ? Visibility.Visible : Visibility.Collapsed;
            SkipBtn.Visibility = (_currentStep < 2) ? Visibility.Visible : Visibility.Collapsed;
            NextBtn.Content = (_currentStep == 2) ? (loc["GetStarted"] ?? loc["Finish"] ?? "Start") : (loc["Next"] ?? "Next");
        }

        private void SaveConfiguredSettings() {
            var s = SettingsService.Instance.Current;
            s.IconShape = _selectedShape;
            s.ThemeColor = _selectedColor;
            s.FolderOpacity = _folderOpacity;
            s.FolderBlurEnabled = _folderBlurEnabled;
            s.FolderBlurRadius = _folderBlurRadius;

            s.PagingMode = _pagingMode;
            s.GridColumns = _gridColumns;
            s.GridRows = _gridRows;
            s.SliderArrowsEnabled = _sliderArrows;
            s.SliderDragEnabled = _sliderDrag;
            s.FocusModeEnabled = _focusMode;
            s.FocusDimmingOpacity = _focusDimming;
            s.FocusBlurEnabled = _focusBlurEnabled;
            s.FocusBlurRadius = _focusBlurRadius;

            SettingsService.Instance.SaveSettings(s);
            PanakManager.Instance.UpdateOpenPopupSettings();
        }

        private bool _isPreviewPinned = false;
        private void PreviewPinBtn_Click(object sender, RoutedEventArgs e) {
            _isPreviewPinned = !_isPreviewPinned;
            var activeBrush = Resources["M3PrimaryBrush"] as Brush ?? Brushes.SkyBlue;
            var normalBrush = Resources["PopupHeaderBtnBg"] as Brush ?? new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
            if (sender is Button btn) {
                btn.Background = _isPreviewPinned ? activeBrush : normalBrush;
            } else if (Step1PreviewPinBtn != null) {
                Step1PreviewPinBtn.Background = _isPreviewPinned ? activeBrush : normalBrush;
            }
        }

        private void Step1ColorIndicator_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                var s = SettingsService.Instance.Current;
                if (!s.CustomPaletteColors.Contains(_selectedColor, StringComparer.OrdinalIgnoreCase)) {
                    s.CustomPaletteColors.Add(_selectedColor);
                    SettingsService.Instance.SaveSettings(s);
                    RebuildStep1CustomSwatchesUI();
                    var anim = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(200));
                    var scale = new ScaleTransform(1, 1);
                    Step1ColorIndicator.RenderTransformOrigin = new Point(0.5, 0.5);
                    Step1ColorIndicator.RenderTransform = scale;
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
            }
        }

        private void RebuildStep1CustomSwatchesUI() {
            if (Step1CustomSwatchesPanel == null) return;
            Step1CustomSwatchesPanel.Children.Clear();
            var s = SettingsService.Instance.Current;
            if (s.CustomPaletteColors == null || s.CustomPaletteColors.Count == 0) return;

            foreach (var hex in s.CustomPaletteColors) {
                string colHex = hex;
                var grid = new Grid {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(3, 0, 3, 0),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };

                var rb = new RadioButton {
                    GroupName = "WizardColorGroup",
                    Style = (Style)FindResource("ColorSwatchStyle"),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colHex)),
                    Tag = colHex,
                    ToolTip = colHex,
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
                        Step1HueSlider.Value = hue;
                    } finally {
                        _isInitializing = false;
                    }
                    UpdateLivePreview();
                };
                grid.Children.Add(rb);

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

                grid.MouseEnter += (snd, ev) => deleteBadge.Visibility = Visibility.Visible;
                grid.MouseLeave += (snd, ev) => deleteBadge.Visibility = Visibility.Collapsed;
                deleteBadge.MouseEnter += (snd, ev) => deleteBadge.Background = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                deleteBadge.MouseLeave += (snd, ev) => deleteBadge.Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 30));

                deleteBadge.PreviewMouseLeftButtonDown += (snd, ev) => {
                    ev.Handled = true;
                    var curSettings = SettingsService.Instance.Current;
                    curSettings.CustomPaletteColors.Remove(colHex);
                    SettingsService.Instance.SaveSettings(curSettings);
                    RebuildStep1CustomSwatchesUI();
                    if (string.Equals(_selectedColor, colHex, StringComparison.OrdinalIgnoreCase)) {
                        string defaultPastel = ShellHelper.DefaultPastelColorHex;
                        _selectedColor = defaultPastel;
                        double hue = ShellHelper.GetHueFromHex(defaultPastel);
                        ApplyMaterialYouPalette(hue);
                        _isInitializing = true;
                        try {
                            Step1HueSlider.Value = hue;
                        } finally {
                            _isInitializing = false;
                        }
                        UpdateLivePreview();
                    }
                };

                grid.Children.Add(deleteBadge);
                Step1CustomSwatchesPanel.Children.Add(grid);
            }
        }

        // Event Handlers for Step 1
        private void ShapeRadio_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender == ShapeCircleRadio) _selectedShape = "Circle";
            else if (sender == ShapeCloverRadio) _selectedShape = "Clover";
            else if (sender == ShapeScallopRadio) _selectedShape = "Scallop";
            else if (sender == ShapeArchRadio) _selectedShape = "Arch";
            else _selectedShape = "Squircle";

            UpdateLivePreview();
        }

        private void WizardColorSwatch_Checked(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.Tag is string hex) {
                _selectedColor = hex;
                double hue = ShellHelper.GetHueFromHex(hex);
                ApplyMaterialYouPalette(hue);
                _isInitializing = true;
                try {
                    Step1HueSlider.Value = hue;
                } finally {
                    _isInitializing = false;
                }
                UpdateLivePreview();
            }
        }

        private void Step1HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing) return;
            _selectedColor = ShellHelper.ColorFromPastelHueHex(e.NewValue);
            ApplyMaterialYouPalette(e.NewValue);
            UpdateLivePreview();
        }

        private void Step1OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _folderOpacity = e.NewValue / 100.0;
            if (Step1OpacityValueText != null) {
                Step1OpacityValueText.Text = $"{(int)e.NewValue}%";
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step1BlurCheckBox_Click(object sender, RoutedEventArgs e) {
            _folderBlurEnabled = Step1BlurCheckBox.IsChecked ?? true;
            if (Step1BlurRadiusSlider != null) {
                Step1BlurRadiusSlider.IsEnabled = _folderBlurEnabled;
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step1BlurRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _folderBlurRadius = e.NewValue;
            if (Step1RadiusValueText != null) {
                Step1RadiusValueText.Text = $"{(int)e.NewValue} px";
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        // Event Handlers for Step 2
        private void Step2Paging_Click(object sender, RoutedEventArgs e) {
            _pagingMode = (Step2ScrollbarRadio.IsChecked == true) ? "Scrollbar" : "Slider";
            RebuildPreviewItems();
            UpdateLivePreview();
        }

        private void Step2ColsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _gridColumns = (int)e.NewValue;
            if (Step2ColsValueText != null) {
                Step2ColsValueText.Text = _gridColumns.ToString();
            }
            if (!_isInitializing) {
                RebuildPreviewItems();
                UpdateLivePreview();
            }
        }

        private void Step2RowsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _gridRows = (int)e.NewValue;
            if (Step2RowsValueText != null) {
                Step2RowsValueText.Text = _gridRows.ToString();
            }
            if (!_isInitializing) {
                RebuildPreviewItems();
                UpdateLivePreview();
            }
        }

        private void Step2FocusCheckBox_Click(object sender, RoutedEventArgs e) {
            _focusMode = Step2FocusCheckBox.IsChecked ?? true;
            if (Step2FocusDimOverlay != null) {
                Step2FocusDimOverlay.Opacity = _focusMode ? _focusDimming : 0.0;
            }
            if (Step2FocusWallpaperBlurFx != null) {
                Step2FocusWallpaperBlurFx.Radius = (_focusMode && _focusBlurEnabled) ? _focusBlurRadius : 0.0;
            }
            UpdateLivePreview();
        }

        private void Step2FocusBlurCheckBox_Click(object sender, RoutedEventArgs e) {
            _focusBlurEnabled = Step2FocusBlurCheckBox.IsChecked ?? true;
            if (Step2FocusBlurSlider != null) {
                Step2FocusBlurSlider.IsEnabled = _focusBlurEnabled;
            }
            if (Step2FocusWallpaperBlurFx != null) {
                Step2FocusWallpaperBlurFx.Radius = (_focusMode && _focusBlurEnabled) ? _focusBlurRadius : 0.0;
            }
            UpdateLivePreview();
        }

        private void Step2DimmingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _focusDimming = e.NewValue / 100.0;
            if (Step2DimmingValueText != null) {
                Step2DimmingValueText.Text = $"{(int)e.NewValue}%";
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step2FocusBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _focusBlurRadius = e.NewValue;
            if (Step2FocusBlurValueText != null) {
                Step2FocusBlurValueText.Text = $"{(int)e.NewValue} px";
            }
            if (Step2FocusWallpaperBlurFx != null) {
                Step2FocusWallpaperBlurFx.Radius = (_focusMode && _focusBlurEnabled) ? _focusBlurRadius : 0.0;
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step2ArrowsCheckBox_Click(object sender, RoutedEventArgs e) {
            _sliderArrows = Step2ArrowsCheckBox.IsChecked ?? true;
            RebuildPreviewItems();
            UpdateLivePreview();
        }

        private void Step2DragCheckBox_Click(object sender, RoutedEventArgs e) {
            _sliderDrag = Step2DragCheckBox.IsChecked ?? true;
        }

        // Navigation Footer Buttons
        private void BackBtn_Click(object sender, RoutedEventArgs e) {
            GoToStep(_currentStep - 1);
        }

        private void SkipBtn_Click(object sender, RoutedEventArgs e) {
            GoToStep(2);
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentStep < 2) {
                GoToStep(_currentStep + 1);
            } else {
                SaveConfiguredSettings();
                Close();
            }
        }

        private void StepDot_MouseDown(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement el && el.Tag is string tag && int.TryParse(tag, out int idx)) {
                GoToStep(idx);
            }
        }

        private void CrowdinBtn_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://crowdin.com/project/panak") { UseShellExecute = true });
            } catch { }
        }

        private void KofiBtn_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/hranthakobian/") { UseShellExecute = true });
            } catch { }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e) {
            SaveConfiguredSettings();
            try {
                if (Application.Current is App app) {
                    app.ShowSettings();
                }
            } catch { }
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            SaveConfiguredSettings();
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                SaveConfiguredSettings();
                Close();
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            if (_liveWallpaperTimer != null) {
                _liveWallpaperTimer.Stop();
                _liveWallpaperTimer = null;
            }
        }
    }
}
