using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
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

        public WelcomeWindow() {
            InitializeComponent();
            LoadLogo();
            LoadInitialSettings();
            ApplyLocalization();
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

        private void ApplyLocalization() {
            var loc = LocalizationService.Instance;

            Title = loc["WelcomeToPanak"];
            Step1TitleText.Text = loc["Step1Title"];
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

            Step2TitleText.Text = loc["Step2Title"];
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

                // Step 1 Preview Card
                if (Step1FolderCardBorder != null) {
                    Step1FolderCardBorder.Background = folderBrush;
                    Step1BlurFx.Radius = _folderBlurEnabled ? Math.Max(2, _folderBlurRadius * 0.5) : 0;
                    Step1FolderBlurUnderlay.Visibility = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
                    ApplyShapeToPreviewBadge(Step1PreviewShapePath, Step1PreviewShapeBadge, _selectedShape);
                }

                // Step 2 Preview Card
                if (Step2FolderCardBorder != null) {
                    Step2FolderCardBorder.Background = folderBrush;
                    Step2FocusDimOverlay.Opacity = _focusMode ? _focusDimming : 0.0;
                    ApplyShapeToPreviewBadge(Step2PreviewShapePath, Step2PreviewShapeBadge, _selectedShape);

                    bool isSlider = string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase);
                    if (Step2PreviewFooterGrid != null) {
                        Step2PreviewFooterGrid.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
                        Step2PreviewArrowLeft.Visibility = _sliderArrows ? Visibility.Visible : Visibility.Collapsed;
                        Step2PreviewArrowRight.Visibility = _sliderArrows ? Visibility.Visible : Visibility.Collapsed;
                    }
                }

                // Step 3 Preview Card
                if (Step3FolderCardBorder != null) {
                    Step3FolderCardBorder.Background = folderBrush;
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

        private void GoToStep(int targetStep, bool animate = true) {
            if (targetStep < 0 || targetStep > 2 || targetStep == _currentStep) return;

            Grid currentGrid = _currentStep switch {
                0 => Step1Grid,
                1 => Step2Grid,
                _ => Step3Grid
            };

            Grid targetGrid = targetStep switch {
                0 => Step1Grid,
                1 => Step2Grid,
                _ => Step3Grid
            };

            _currentStep = targetStep;

            if (animate) {
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180));
                fadeOut.Completed += (s, e) => {
                    currentGrid.Visibility = Visibility.Collapsed;
                    targetGrid.Visibility = Visibility.Visible;
                    var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));
                    targetGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };
                currentGrid.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            } else {
                currentGrid.Visibility = Visibility.Collapsed;
                targetGrid.Visibility = Visibility.Visible;
                targetGrid.Opacity = 1.0;
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

            StepIndicatorText.Text = $"Step {_currentStep + 1} of 3";
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
            UpdateLivePreview();
        }

        private void Step2ColsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _gridColumns = (int)e.NewValue;
            if (Step2ColsValueText != null) {
                Step2ColsValueText.Text = _gridColumns.ToString();
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step2RowsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _gridRows = (int)e.NewValue;
            if (Step2RowsValueText != null) {
                Step2RowsValueText.Text = _gridRows.ToString();
            }
            if (!_isInitializing) UpdateLivePreview();
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
    }
}
