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

        private DispatcherTimer? _liveWallpaperTimer;
        private bool _isPreviewOpen = true;

        private int _currentPreviewPage = 0;
        private int _totalPreviewPages = 1;
        private double _previewPageWidth = 270.0;
        private bool _isDraggingSlider = false;
        private Point _dragStartPos;

        private static readonly (string Name, string Icon)[] MockItems = new[] {
            ("Project Plan.docx", "📄"),
            ("Budget 2026.xlsx", "📊"),
            ("Banner_Final.png", "🖼️"),
            ("Notes.txt", "📝"),
            ("AppSetup.exe", "⚙️"),
            ("Client Brief.pdf", "📑"),
            ("DesignSystem.fig", "🎨"),
            ("Podcast_Ep4.mp3", "🎵"),
            ("ReleaseDemo.mp4", "🎬"),
            ("SourceBackup.zip", "📦"),
            ("Architecture.drawio", "📐"),
            ("Script.ps1", "💻"),
            ("AppIcon.svg", "💠"),
            ("Summary.md", "📋"),
            ("Financials.csv", "📈"),
            ("Database.sql", "🗄️"),
            ("Contract.pdf", "📃"),
            ("Wireframes.xd", "📱")
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
            Step1BlurCheckBox.IsChecked = _folderBlurEnabled;
            Step1BlurRadiusSlider.Value = _folderBlurRadius;
            Step1RadiusValueText.Text = $"{(int)_folderBlurRadius} px";
            Step1BlurRadiusContainer.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;

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
            if (Step2WallpaperImage != null) Step2WallpaperImage.Source = src;
            if (Step2BlurredWallpaperImage != null) Step2BlurredWallpaperImage.Source = src;
            if (Step3WallpaperImage != null) Step3WallpaperImage.Source = src;
            if (Step3BlurredWallpaperImage != null) Step3BlurredWallpaperImage.Source = src;
        }

        private void ApplyLocalization() {
            var loc = LocalizationService.Instance;

            Title = loc["WelcomeToPanak"];
            StepIndicatorText.Text = $"{loc["Step"]} {_currentStep + 1} / 3";
            Step1TitleText.Text = $"{loc["Step"]} 1: {loc["Step1Title"]}";
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
            Step1RadiusLabel.Text = loc["FolderBlurRadius"];
            Step1LivePreviewBadge.Text = loc["LivePreview"];

            Step2TitleText.Text = $"{loc["Step"]} 2: {loc["Step2Title"]}";
            Step2SubtitleText.Text = loc["Step2Subtitle"];
            Step2PagingModeLabel.Text = loc["PagingMode"];
            Step2SliderRadio.Content = loc["PagingModeSlider"];
            Step2ScrollbarRadio.Content = loc["PagingModeScrollbar"];
            Step2ColsLabel.Text = loc["ItemsPerRow"];
            Step2RowsLabel.Text = loc["GridRows"];
            Step2FocusModeLabel.Text = loc["FocusMode"];
            Step2DimmingLabel.Text = loc["FocusDimming"];
            Step2ArrowsLabel.Text = loc["SliderArrows"];
            Step2DragLabel.Text = loc["SliderDrag"];

            Step3TitleText.Text = loc["WelcomeToPanak"];
            Step3SubtitleText.Text = loc["WelcomeSubtitle"];
            Feature1Text.Text = loc["WelcomeFeature1"];
            Feature2Text.Text = loc["WelcomeFeature2"];
            Feature3Text.Text = loc["WelcomeFeature3"];
            SettingsBtn.Content = "⚙️ " + loc["Settings"];
            KofiBtn.Content = loc["DonateOnKofi"];

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
            int totalItems = MockItems.Length;
            int actualCols = Math.Clamp(Math.Min(totalItems, _gridColumns), 1, 8);
            int actualRows = Math.Clamp(Math.Min((int)Math.Ceiling((double)totalItems / actualCols), _gridRows), 1, 5);

            double contentW = actualCols * 70.0;
            double contentH = actualRows * 78.0;
            _previewPageWidth = contentW;

            double popupW = Math.Max(240, contentW + 20.0);
            double popupH = Math.Max(180, contentH + 82.0);

            if (Step1BlurUnderlay != null) { Step1BlurUnderlay.Width = popupW; Step1BlurUnderlay.Height = popupH; }
            if (Step1FolderCardBorder != null) { Step1FolderCardBorder.Width = popupW; Step1FolderCardBorder.Height = popupH; }
            if (Step2BlurUnderlay != null) { Step2BlurUnderlay.Width = popupW; Step2BlurUnderlay.Height = popupH; }
            if (Step2FolderCardBorder != null) { Step2FolderCardBorder.Width = popupW; Step2FolderCardBorder.Height = popupH; }

            bool isSlider = string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase);

            if (Step1SliderViewport != null && Step1ScrollViewer != null && Step1FooterPagerGrid != null) {
                Step1SliderViewport.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
                Step1ScrollViewer.Visibility = isSlider ? Visibility.Collapsed : Visibility.Visible;
                Step1FooterPagerGrid.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
                Step1PrevBtn.Visibility = _sliderArrows ? Visibility.Visible : Visibility.Collapsed;
                Step1NextBtn.Visibility = _sliderArrows ? Visibility.Visible : Visibility.Collapsed;
            }

            if (Step2SliderViewport != null && Step2ScrollViewer != null && Step2FooterPagerGrid != null) {
                Step2SliderViewport.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
                Step2ScrollViewer.Visibility = isSlider ? Visibility.Collapsed : Visibility.Visible;
                Step2FooterPagerGrid.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
                Step2PrevBtn.Visibility = _sliderArrows ? Visibility.Visible : Visibility.Collapsed;
                Step2NextBtn.Visibility = _sliderArrows ? Visibility.Visible : Visibility.Collapsed;
            }

            int itemsPerPage = Math.Max(1, actualCols * actualRows);
            _totalPreviewPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);
            if (_currentPreviewPage >= _totalPreviewPages) _currentPreviewPage = _totalPreviewPages - 1;
            if (_currentPreviewPage < 0) _currentPreviewPage = 0;

            BuildSliderPages(Step1SliderPagesPanel, actualCols, actualRows, itemsPerPage);
            BuildSliderPages(Step2SliderPagesPanel, actualCols, actualRows, itemsPerPage);

            BuildScrollGrid(Step1ScrollGrid, actualCols);
            BuildScrollGrid(Step2ScrollGrid, actualCols);

            UpdatePreviewDots(Step1DotsPanel);
            UpdatePreviewDots(Step2DotsPanel);
            AnimatePreviewPage(false);
        }

        private FrameworkElement CreateMockItemView(string name, string icon) {
            var border = new Border {
                Width = 68,
                Height = 76,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Color.FromArgb(35, 128, 128, 128));
            border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;

            var sp = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconBorder = new Border {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(10),
                Background = Resources["M3SurfaceBrush"] as Brush ?? new SolidColorBrush(Color.FromRgb(254, 244, 235)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var iconText = new TextBlock {
                Text = icon,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;

            var nameText = new TextBlock {
                Text = name,
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = Resources["M3OnSurfaceBrush"] as Brush ?? Brushes.Black,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 64
            };

            sp.Children.Add(iconBorder);
            sp.Children.Add(nameText);
            border.Child = sp;
            return border;
        }

        private void BuildSliderPages(StackPanel? panel, int cols, int rows, int itemsPerPage) {
            if (panel == null) return;
            panel.Children.Clear();

            for (int p = 0; p < _totalPreviewPages; p++) {
                var grid = new UniformGrid {
                    Columns = cols,
                    Rows = rows,
                    Width = cols * 70.0,
                    Height = rows * 78.0,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                int startIndex = p * itemsPerPage;
                int count = Math.Min(itemsPerPage, MockItems.Length - startIndex);
                for (int i = 0; i < count; i++) {
                    var item = MockItems[startIndex + i];
                    grid.Children.Add(CreateMockItemView(item.Name, item.Icon));
                }

                panel.Children.Add(grid);
            }
        }

        private void BuildScrollGrid(UniformGrid? grid, int cols) {
            if (grid == null) return;
            grid.Children.Clear();
            grid.Columns = cols;
            grid.Width = cols * 70.0;
            foreach (var item in MockItems) {
                grid.Children.Add(CreateMockItemView(item.Name, item.Icon));
            }
        }

        private void UpdatePreviewDots(StackPanel? panel) {
            if (panel == null) return;
            panel.Children.Clear();

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
                    UpdatePreviewDots(Step1DotsPanel);
                    UpdatePreviewDots(Step2DotsPanel);
                };
                panel.Children.Add(dot);
            }
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
                if (Step2PagesTranslate != null) Step2PagesTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
            } else {
                if (Step1PagesTranslate != null) {
                    Step1PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    Step1PagesTranslate.X = targetX;
                }
                if (Step2PagesTranslate != null) {
                    Step2PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    Step2PagesTranslate.X = targetX;
                }
            }
        }

        private void PreviewSlider_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase)) {
                if (e.Delta < 0 && _currentPreviewPage < _totalPreviewPages - 1) {
                    _currentPreviewPage++;
                    AnimatePreviewPage(true);
                    UpdatePreviewDots(Step1DotsPanel);
                    UpdatePreviewDots(Step2DotsPanel);
                    e.Handled = true;
                } else if (e.Delta > 0 && _currentPreviewPage > 0) {
                    _currentPreviewPage--;
                    AnimatePreviewPage(true);
                    UpdatePreviewDots(Step1DotsPanel);
                    UpdatePreviewDots(Step2DotsPanel);
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

        private void PreviewSlider_MouseMove(object sender, MouseEventArgs e) { }

        private void PreviewSlider_MouseUp(object sender, MouseButtonEventArgs e) {
            if (_isDraggingSlider) {
                _isDraggingSlider = false;
                if (sender is UIElement el) el.ReleaseMouseCapture();
                var endPos = e.GetPosition(this);
                double deltaX = endPos.X - _dragStartPos.X;
                if (deltaX < -30 && _currentPreviewPage < _totalPreviewPages - 1) {
                    _currentPreviewPage++;
                    AnimatePreviewPage(true);
                    UpdatePreviewDots(Step1DotsPanel);
                    UpdatePreviewDots(Step2DotsPanel);
                } else if (deltaX > 30 && _currentPreviewPage > 0) {
                    _currentPreviewPage--;
                    AnimatePreviewPage(true);
                    UpdatePreviewDots(Step1DotsPanel);
                    UpdatePreviewDots(Step2DotsPanel);
                }
            }
        }

        private void Step1PrevBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentPreviewPage > 0) {
                _currentPreviewPage--;
                AnimatePreviewPage(true);
                UpdatePreviewDots(Step1DotsPanel);
                UpdatePreviewDots(Step2DotsPanel);
            }
        }

        private void Step1NextBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentPreviewPage < _totalPreviewPages - 1) {
                _currentPreviewPage++;
                AnimatePreviewPage(true);
                UpdatePreviewDots(Step1DotsPanel);
                UpdatePreviewDots(Step2DotsPanel);
            }
        }

        private void DesktopIcon_MouseDown(object sender, MouseButtonEventArgs e) {
            TogglePopupVisibility();
        }

        private void PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            if (_isPreviewOpen) {
                TogglePopupVisibility();
            }
        }

        private void TogglePopupVisibility() {
            if (Step1PopupContainer == null || Step1PopupScale == null) return;

            if (_isPreviewOpen) {
                var scaleAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180));
                scaleAnim.Completed += (s, e) => {
                    Step1PopupContainer.Visibility = Visibility.Collapsed;
                };
                Step1PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                Step1PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                Step1PopupContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                _isPreviewOpen = false;
            } else {
                Step1PopupContainer.Visibility = Visibility.Visible;
                var scaleAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                Step1PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                Step1PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                Step1PopupContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                _isPreviewOpen = true;
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

                // Step 1
                if (Step1FolderCardBorder != null) {
                    Step1FolderCardBorder.Background = folderBrush;
                    Step1FolderBlurFx.Radius = _folderBlurRadius;
                    Step1BlurUnderlay.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    ApplyShapeToPreviewBadge(Step1PreviewShapePath, Step1PreviewShapeBadge, _selectedShape);
                    ApplyShapeToDesktopIconPlate(Step1DesktopIconShapePath, Step1DesktopIconPlate, _selectedShape);
                }

                // Step 2
                if (Step2FolderCardBorder != null) {
                    Step2FolderCardBorder.Background = folderBrush;
                    Step2FolderBlurFx.Radius = _folderBlurRadius;
                    Step2BlurUnderlay.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    Step2FocusDimOverlay.Opacity = _focusMode ? _focusDimming : 0.0;
                    ApplyShapeToPreviewBadge(Step2PreviewShapePath, Step2PreviewShapeBadge, _selectedShape);
                }

                // Step 3
                if (Step3FolderCardBorder != null) {
                    Step3FolderCardBorder.Background = folderBrush;
                    Step3FolderBlurFx.Radius = _folderBlurRadius;
                    Step3BlurUnderlay.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    ApplyShapeToPreviewBadge(Step3PreviewShapePath, Step3PreviewShapeBadge, _selectedShape);
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

            SettingsService.Instance.SaveSettings(s);
            PanakManager.Instance.UpdateOpenPopupSettings();
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
            Step1BlurRadiusContainer.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
            UpdateLivePreview();
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
            UpdateLivePreview();
        }

        private void Step2DimmingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _focusDimming = e.NewValue / 100.0;
            if (Step2DimmingValueText != null) {
                Step2DimmingValueText.Text = $"{(int)e.NewValue}%";
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
