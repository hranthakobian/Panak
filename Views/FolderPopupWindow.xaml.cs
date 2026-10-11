using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Panak.Models;
using Panak.Services;

namespace Panak.Views {
    public partial class FolderPopupWindow : Window {
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private string _rootFolderPath = string.Empty;
        private string _currentFolderPath = string.Empty;
        private PanakFolderData _folderData = new PanakFolderData();
        private Rect _iconScreenBounds = Rect.Empty;
        private Point _dragStartPoint;
        private Point _transformOrigin = new Point(0, 0);
        private Point _anchorScreenPoint = new Point(0, 0);
        private bool _isClosing = false;
        private bool _isOpening = false;
        private bool _isInitializing = false;
        private DateTime _openedTime = DateTime.MinValue;
        private int _animationSessionId = 0;
        private UniformGrid? _itemsUniformGrid;
        private bool _isCurrentThemeDark = true;

        public bool IsWithinOpenGracePeriod() {
            return _isOpening || (DateTime.UtcNow - _openedTime).TotalMilliseconds < 450;
        }

        public ObservableCollection<PanakItem> Items { get; } = new ObservableCollection<PanakItem>();
        public event Action? ClosedAnimated;
        public event Action? OpeningAnimationStarting;

        public FolderPopupWindow() {
            InitializeComponent();
            FolderItemsControl.ItemsSource = Items;
            FolderItemsControl.Loaded += (s, e) => ApplySettings();
            ApplyLocalization();

            SettingsService.Instance.SettingsChanged += OnSettingsChanged;
            Closed += (s, e) => {
                SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
            };
        }

        private void OnSettingsChanged(AppSettings s) {
            Dispatcher.Invoke(() => {
                ApplyThemeMode(s.ThemeMode ?? "Auto");
                if (_itemsUniformGrid != null) {
                    _itemsUniformGrid.Columns = GetConfiguredColumns();
                }
            });
        }

        private void ItemsUniformGrid_Loaded(object sender, RoutedEventArgs e) {
            if (sender is UniformGrid grid) {
                _itemsUniformGrid = grid;
                grid.Columns = GetConfiguredColumns();
            }
        }

        public int GetConfiguredColumns() {
            int columns = SettingsService.Instance.Current.GridColumns;
            if (columns < 3) {
                columns = 3;
            }
            if (columns > 8) {
                columns = 8;
            }
            if (Items.Count > 0 && Items.Count < columns) {
                return Items.Count;
            }
            return columns;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (msg == WM_WINDOWPOSCHANGING && !_isOpening && !_isClosing) {
                var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                if ((pos.flags & SWP_NOSIZE) == 0 && pos.cx > 0 && pos.cy > 0) {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    double dpiX = dpi.DpiScaleX;
                    double dpiY = dpi.DpiScaleY;

                    var workArea = SystemParameters.WorkArea;
                    int screenLeft = (int)Math.Round(workArea.Left * dpiX);
                    int screenTop = (int)Math.Round(workArea.Top * dpiY);
                    int screenRight = (int)Math.Round(workArea.Right * dpiX);
                    int screenBottom = (int)Math.Round(workArea.Bottom * dpiY);

                    int targetX;
                    int targetY;

                    if (SettingsService.Instance.Current.OpenInCenterOnly) {
                        targetX = screenLeft + (screenRight - screenLeft - pos.cx) / 2;
                        targetY = screenTop + (screenBottom - screenTop - pos.cy) / 2;
                    } else {
                        int anchorX = (int)Math.Round(_anchorScreenPoint.X * dpiX);
                        int anchorY = (int)Math.Round(_anchorScreenPoint.Y * dpiY);

                        targetX = (_transformOrigin.X > 0.5) ? (anchorX - pos.cx) : anchorX;
                        targetY = (_transformOrigin.Y > 0.5) ? (anchorY - pos.cy) : anchorY;

                        int margin = (int)Math.Round(8 * dpiX);
                        if (targetX < screenLeft + margin) targetX = screenLeft + margin;
                        if (targetX + pos.cx > screenRight - margin) targetX = screenRight - pos.cx - margin;
                        if (targetY < screenTop + margin) targetY = screenTop + margin;
                        if (targetY + pos.cy > screenBottom - margin) targetY = screenBottom - pos.cy - margin;
                    }

                    pos.x = targetX;
                    pos.y = targetY;
                    pos.flags &= ~SWP_NOMOVE;

                    Marshal.StructureToPtr(pos, lParam, false);
                }
            }
            return IntPtr.Zero;
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Make WPF render target transparent so DWM backdrop shines through
            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget != null) {
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
            source?.AddHook(HwndHook);

            // Apply Windows dark mode attribute
            try {
                int dark = 1; // Immersive dark mode
                ShellHelper.DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            } catch {
                // Ignore DWM errors on older Windows versions
            }
        }

        protected override void OnDeactivated(EventArgs e) {
            base.OnDeactivated(e);
            if (IsWithinOpenGracePeriod()) {
                return;
            }
            if (!_isClosing) {
                CloseAnimated();
            }
        }

        public void ApplyLocalization() {
            var loc = LocalizationService.Instance;
            EmptyTitleText.Text = loc["EmptyFolder"];
            EmptySubtext.Text = loc["DropFilesHere"];
            EditNameLabel.Text = loc["FolderTitle"];
            ColorThemeLabel.Text = loc["ColorTheme"];
            OpacitySlider.ToolTip = loc["Opacity"];
            ShapeRadioCircle.ToolTip = loc["Circle"];
            ShapeRadioSquircle.ToolTip = loc["Squircle"];
            ShapeRadioClover.ToolTip = loc["Clover"];
            ShapeRadioScallop.ToolTip = loc["Scallop"];
            ShapeRadioArch.ToolTip = loc["Arch"];
            AlwaysPanakCheckBox.Content = loc["AlwaysOpenWithPanak"];
            EditButton.ToolTip = loc["Rename"];
            ExplorerButton.ToolTip = loc["OpenInExplorer"];
            CloseButton.ToolTip = loc["Cancel"];
        }

        public void LoadFolder(string folderPath, Rect iconBounds) {
            _rootFolderPath = folderPath;
            _currentFolderPath = folderPath;
            _iconScreenBounds = iconBounds;
            _isClosing = false;
            _isOpening = true;
            _openedTime = DateTime.UtcNow;
            _animationSessionId++;

            // Cancel any in-flight animations and reset transforms
            RootContainer.BeginAnimation(OpacityProperty, null);
            PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PopupTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            PopupTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            PopupTranslate.X = 0;
            PopupTranslate.Y = 0;
            RootContainer.Opacity = 1.0;
            PopupScale.ScaleX = 1.0;
            PopupScale.ScaleY = 1.0;

            LoadMetadata();
            RefreshItemsList();
            ApplySettings();

            _isInitializing = true;
            try {
                // Set Title & Count
                FolderTitleText.Text = _folderData.Title;
                FolderTitleText.Visibility = Visibility.Visible;
                InlineNameEditBox.Visibility = Visibility.Collapsed;
                UpdateItemCountText();
                UpdateBreadcrumbs();

                // Apply Theme Color
                ApplyThemeColor(_folderData.Color);

                // Set default/accent pastel color on first swatch dynamically
                string defaultPastel = ShellHelper.DefaultPastelColorHex;
                AccentSwatch.Tag = defaultPastel;
                try {
                    var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(defaultPastel);
                    AccentSwatch.Background = new SolidColorBrush(c);
                } catch {
                    // Fallback
                }

                // Check matching color swatch in EditPanel
                bool foundSwatch = false;
                foreach (var child in ColorSwatchesPanel.Children) {
                    if (child is RadioButton rb && rb.Tag is string tag) {
                        bool isMatch = string.Equals(tag, _folderData.Color, StringComparison.OrdinalIgnoreCase);
                        rb.IsChecked = isMatch;
                        if (isMatch) {
                            foundSwatch = true;
                        }
                    }
                }
                if (!foundSwatch) {
                    double folderHue = ShellHelper.GetHueFromHex(_folderData.Color);
                    foreach (var child in ColorSwatchesPanel.Children) {
                        if (child is RadioButton rb && rb.Tag is string tag) {
                            double swHue = ShellHelper.GetHueFromHex(tag);
                            double diff = Math.Abs(swHue - folderHue);
                            if (diff > 180) diff = 360 - diff;
                            if (diff < 4.0) {
                                rb.IsChecked = true;
                                foundSwatch = true;
                                break;
                            }
                        }
                    }
                }

                // Initialize Hue Slider position
                HueSlider.Value = ShellHelper.GetHueFromHex(_folderData.Color);

                // Initialize Opacity Slider position
                double op = _folderData.BackgroundOpacity > 0 ? _folderData.BackgroundOpacity : 0.85;
                OpacitySlider.Value = op;
                ApplyOpacity(op);

                // Apply global theme mode (Dark / Light / Auto)
                ApplyThemeMode(SettingsService.Instance.Current.ThemeMode ?? "Auto");

                // Select matching shape in 5-shape selector
                string currentShape = _folderData.IconShape;
                if (string.Equals(currentShape, "Circle", StringComparison.OrdinalIgnoreCase)) {
                    ShapeRadioCircle.IsChecked = true;
                } else if (string.Equals(currentShape, "Clover", StringComparison.OrdinalIgnoreCase)) {
                    ShapeRadioClover.IsChecked = true;
                } else if (string.Equals(currentShape, "Scallop", StringComparison.OrdinalIgnoreCase)) {
                    ShapeRadioScallop.IsChecked = true;
                } else if (string.Equals(currentShape, "Arch", StringComparison.OrdinalIgnoreCase) || string.Equals(currentShape, "Teardrop", StringComparison.OrdinalIgnoreCase)) {
                    ShapeRadioArch.IsChecked = true;
                } else {
                    ShapeRadioSquircle.IsChecked = true;
                }

                NameEditBox.Text = _folderData.Title;
                AlwaysPanakCheckBox.IsChecked = PanakManager.Instance.IsPanakFolder(_rootFolderPath);
                CloseEditPanelImmediate();
            } finally {
                _isInitializing = false;
            }

            // Position & play opening animation
            Dispatcher.InvokeAsync(() => {
                ApplySettings();
                UpdateLayout();
                PositionAndAnimateIn();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void NavigateTo(string path) {
            if (!Directory.Exists(path)) {
                return;
            }
            _currentFolderPath = path;
            RefreshItemsList();
            UpdateBreadcrumbs();
        }

        public void NavigateUp() {
            if (string.Equals(_currentFolderPath, _rootFolderPath, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            string? parent = Path.GetDirectoryName(_currentFolderPath);
            if (!string.IsNullOrEmpty(parent) && parent.StartsWith(_rootFolderPath, StringComparison.OrdinalIgnoreCase)) {
                NavigateTo(parent);
            } else {
                NavigateTo(_rootFolderPath);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) {
            NavigateUp();
        }

        private void UpdateBreadcrumbs() {
            bool isSub = !string.Equals(_currentFolderPath, _rootFolderPath, StringComparison.OrdinalIgnoreCase);
            BackButton.Visibility = isSub ? Visibility.Visible : Visibility.Collapsed;
            BreadcrumbsPanel.Children.Clear();

            if (isSub && _currentFolderPath.StartsWith(_rootFolderPath, StringComparison.OrdinalIgnoreCase)) {
                string rel = _currentFolderPath.Substring(_rootFolderPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string[] parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                string accum = _rootFolderPath;
                for (int i = 0; i < parts.Length; i++) {
                    string part = parts[i];
                    accum = Path.Combine(accum, part);
                    string targetPath = accum;

                    var sep = new TextBlock {
                        Text = "›",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(_isCurrentThemeDark ? System.Windows.Media.Color.FromArgb(120, 255, 255, 255) : System.Windows.Media.Color.FromArgb(120, 15, 23, 42)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(3, 0, 3, 0)
                    };
                    BreadcrumbsPanel.Children.Add(sep);

                    bool isLast = (i == parts.Length - 1);
                    var btn = new Button {
                        Content = part,
                        Style = (Style)FindResource("BreadcrumbButtonStyle"),
                        Foreground = new SolidColorBrush(isLast
                            ? (_isCurrentThemeDark ? Colors.White : System.Windows.Media.Color.FromRgb(15, 23, 42))
                            : (_isCurrentThemeDark ? System.Windows.Media.Color.FromArgb(190, 203, 213, 225) : System.Windows.Media.Color.FromArgb(190, 71, 85, 105))),
                        FontWeight = isLast ? FontWeights.SemiBold : FontWeights.Normal
                    };
                    btn.Click += (s, e) => NavigateTo(targetPath);
                    BreadcrumbsPanel.Children.Add(btn);
                }
            }
        }

        private void ApplySettings() {
            int columns = GetConfiguredColumns();

            if (_itemsUniformGrid != null) {
                _itemsUniformGrid.Columns = columns;
                return;
            }

            // Find UniformGrid in template or children
            var uniformGrid = FindVisualChild<UniformGrid>(FolderItemsControl);
            if (uniformGrid != null) {
                _itemsUniformGrid = uniformGrid;
                uniformGrid.Columns = columns;
            }
        }

        private void LoadMetadata() {
            _folderData = PanakFolderData.Load(_rootFolderPath);
        }

        private void SaveMetadata() {
            if (_isInitializing) {
                return;
            }
            _folderData.Save(_rootFolderPath);
        }

        public void RefreshItemsList() {
            double oldContentHeight = (IsLoaded && !_isOpening && !_isClosing && ContentAreaGrid.IsVisible)
                ? ContentAreaGrid.ActualHeight
                : -1;

            Items.Clear();

            if (!Directory.Exists(_currentFolderPath)) {
                EmptyStatePanel.Visibility = Visibility.Visible;
                ItemsScrollViewer.Visibility = Visibility.Collapsed;
                return;
            }

            try {
                var dirInfo = new DirectoryInfo(_currentFolderPath);
                var entries = dirInfo.GetFileSystemInfos()
                    .Where(f => !f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.Equals("panak.json", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                                !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .OrderBy(f => !f.Attributes.HasFlag(FileAttributes.Directory))
                    .ThenBy(f => f.Name)
                    .ToList();

                foreach (var entry in entries) {
                    bool isDir = entry.Attributes.HasFlag(FileAttributes.Directory);
                    string sizeText = isDir ? "Folder" : FormatFileSize(((FileInfo)entry).Length);

                    var item = new PanakItem {
                        FullPath = entry.FullName,
                        DisplayName = entry.Name,
                        Extension = entry.Extension,
                        IsDirectory = isDir,
                        IconSource = ShellHelper.GetFileIconSource(entry.FullName, isLarge: true),
                        SizeDisplay = sizeText,
                        LastModified = entry.LastWriteTime
                    };
                    Items.Add(item);
                }

                bool hasItems = Items.Count > 0;
                EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
                ItemsScrollViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
                UpdateItemCountText();
                ApplySettings();

                if (oldContentHeight > 10 && IsLoaded && !_isOpening && !_isClosing) {
                    AnimateContentAreaResize(oldContentHeight);
                }
            } catch {
                EmptyStatePanel.Visibility = Visibility.Visible;
                ItemsScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        private void AnimateContentAreaResize(double oldHeight) {
            ContentAreaGrid.UpdateLayout();
            ContentAreaGrid.Measure(new Size(ActualWidth > 0 ? ActualWidth - 24 : 350, 380));
            double targetHeight = Math.Min(380, ContentAreaGrid.DesiredSize.Height);

            if (Math.Abs(targetHeight - oldHeight) > 4) {
                Duration duration = new Duration(TimeSpan.FromMilliseconds(200));
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var heightAnim = new DoubleAnimation(oldHeight, targetHeight, duration) { EasingFunction = ease };
                var opacityAnim = new DoubleAnimation(0.5, 1.0, duration) { EasingFunction = ease };

                heightAnim.Completed += (s, e) => {
                    ContentAreaGrid.BeginAnimation(HeightProperty, null);
                    ContentAreaGrid.Height = double.NaN;
                };

                ContentAreaGrid.BeginAnimation(HeightProperty, heightAnim);
                ContentAreaGrid.BeginAnimation(OpacityProperty, opacityAnim);
            }
        }

        private void UpdateItemCountText() {
            var loc = LocalizationService.Instance;
            ItemCountBadge.Text = $"{Items.Count} {loc["FilesCount"]}";
        }

        private string FormatFileSize(long bytes) {
            if (bytes >= 1024 * 1024 * 1024) {
                return $"{(bytes / (1024.0 * 1024 * 1024)):0.1} GB";
            }
            if (bytes >= 1024 * 1024) {
                return $"{(bytes / (1024.0 * 1024)):0.1} MB";
            }
            if (bytes >= 1024) {
                return $"{(bytes / 1024.0):0} KB";
            }
            return $"{bytes} B";
        }

        public void ApplyThemeColor(string hexColor) {
            try {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                // Vibrant, rich tint layer (~38% opacity)
                TintLayer.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(95, color.R, color.G, color.B));

                // Thin sleek scrollbar thumb matching folder theme color
                var thumbColor = System.Windows.Media.Color.FromArgb(150, color.R, color.G, color.B);
                var hoverColor = System.Windows.Media.Color.FromArgb(220, color.R, color.G, color.B);
                if (Resources["PanakScrollBarThumbBrush"] is SolidColorBrush tb && !tb.IsFrozen) {
                    tb.Color = thumbColor;
                } else {
                    Resources["PanakScrollBarThumbBrush"] = new SolidColorBrush(thumbColor);
                }
                if (Resources["PanakScrollBarThumbHoverBrush"] is SolidColorBrush hb && !hb.IsFrozen) {
                    hb.Color = hoverColor;
                } else {
                    Resources["PanakScrollBarThumbHoverBrush"] = new SolidColorBrush(hoverColor);
                }

                Resources["CurrentHueThumbBrush"] = new SolidColorBrush(color);

                // Material 3 & Dynamic Shape Switcher palette derived from folder color
                double hue = ShellHelper.GetHueFromHex(hexColor);
                var (surfaceHex, containerHex, primaryHex) = ShellHelper.GetMaterialYouTonalPaletteHex(hue);

                var primaryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(primaryHex);
                var containerColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(containerHex);

                // Dynamic shape capsule container background: frosted glass tint matching the folder hue
                var capsuleBg = System.Windows.Media.Color.FromArgb(45, primaryColor.R, primaryColor.G, primaryColor.B);
                var unselectedBg = System.Windows.Media.Color.FromArgb(50, containerColor.R, containerColor.G, containerColor.B);
                var unselectedPath = System.Windows.Media.Color.FromArgb(220, 255, 255, 255);

                Resources["M3PrimaryBrush"] = new SolidColorBrush(primaryColor);
                Resources["M3OnPrimaryBrush"] = new SolidColorBrush(System.Windows.Media.Colors.White);

                Resources["ShapeCapsuleBgBrush"] = new SolidColorBrush(capsuleBg);
                Resources["ShapeUnselectedBgBrush"] = new SolidColorBrush(unselectedBg);
                Resources["ShapeSelectedBgBrush"] = new SolidColorBrush(primaryColor);
                Resources["ShapeUnselectedPathBrush"] = new SolidColorBrush(unselectedPath);
                Resources["ShapeSelectedPathBrush"] = new SolidColorBrush(System.Windows.Media.Colors.White);

                // Dynamic Opacity Slider track gradient matching folder theme color
                var opacityGradient = new LinearGradientBrush {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0)
                };
                opacityGradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(35, primaryColor.R, primaryColor.G, primaryColor.B), 0.0));
                opacityGradient.GradientStops.Add(new GradientStop(primaryColor, 1.0));
                Resources["OpacityTrackBrush"] = opacityGradient;
            } catch {
                // Fallback
            }
        }

        private void PositionAndAnimateIn() {
            UpdateLayout();

            if (_iconScreenBounds.IsEmpty) {
                _iconScreenBounds = ExplorerHelper.GetBoundingBoxForPath(_currentFolderPath);
            }

            double screenLeft = SystemParameters.WorkArea.Left;
            double screenTop = SystemParameters.WorkArea.Top;
            double screenRight = SystemParameters.WorkArea.Right;
            double screenBottom = SystemParameters.WorkArea.Bottom;

            double popupW = ActualWidth > 0 ? ActualWidth : MinWidth;
            double popupH = ActualHeight > 0 ? ActualHeight : MinHeight;

            double targetLeft;
            double targetTop;
            Point originPoint;

            if (SettingsService.Instance.Current.OpenInCenterOnly) {
                targetLeft = screenLeft + (screenRight - screenLeft - popupW) / 2.0;
                targetTop = screenTop + (screenBottom - screenTop - popupH) / 2.0;
                originPoint = new Point(0.5, 0.5);
            } else if (!_iconScreenBounds.IsEmpty && _iconScreenBounds.Width > 0) {
                double iconX = _iconScreenBounds.Left;
                double iconY = _iconScreenBounds.Top;

                bool isRight = iconX > (screenLeft + screenRight) / 2.0;
                bool isBottom = iconY > (screenTop + screenBottom) / 2.0;

                if (!isRight && !isBottom) {
                    // Top-Left quadrant: anchor at Top-Left corner directly over icon
                    targetLeft = iconX - 8;
                    targetTop = iconY - 8;
                    originPoint = new Point(0.0, 0.0);
                } else if (isRight && !isBottom) {
                    // Top-Right quadrant: anchor at Top-Right corner directly over icon
                    targetLeft = _iconScreenBounds.Right + 8 - popupW;
                    targetTop = iconY - 8;
                    originPoint = new Point(1.0, 0.0);
                } else if (!isRight && isBottom) {
                    // Bottom-Left quadrant: anchor at Bottom-Left corner directly over icon
                    targetLeft = iconX - 8;
                    targetTop = _iconScreenBounds.Bottom + 8 - popupH;
                    originPoint = new Point(0.0, 1.0);
                } else {
                    // Bottom-Right quadrant: anchor at Bottom-Right corner directly over icon
                    targetLeft = _iconScreenBounds.Right + 8 - popupW;
                    targetTop = _iconScreenBounds.Bottom + 8 - popupH;
                    originPoint = new Point(1.0, 1.0);
                }
            } else {
                if (ShellHelper.GetCursorPos(out var pt)) {
                    bool isRight = pt.X > (screenLeft + screenRight) / 2.0;
                    bool isBottom = pt.Y > (screenTop + screenBottom) / 2.0;
                    targetLeft = isRight ? pt.X - popupW : pt.X;
                    targetTop = isBottom ? pt.Y - popupH : pt.Y;
                    originPoint = new Point(isRight ? 1.0 : 0.0, isBottom ? 1.0 : 0.0);
                } else {
                    targetLeft = (screenLeft + screenRight - popupW) / 2.0;
                    targetTop = (screenTop + screenBottom - popupH) / 2.0;
                    originPoint = new Point(0.5, 0.5);
                }
            }

            // Clamp strictly within work area
            if (targetLeft < screenLeft + 8) {
                targetLeft = screenLeft + 8;
            }
            if (targetLeft + popupW > screenRight - 8) {
                targetLeft = screenRight - popupW - 8;
            }
            if (targetTop < screenTop + 8) {
                targetTop = screenTop + 8;
            }
            if (targetTop + popupH > screenBottom - 8) {
                targetTop = screenBottom - popupH - 8;
            }

            Left = targetLeft;
            Top = targetTop;

            _transformOrigin = originPoint;
            RootContainer.RenderTransformOrigin = originPoint;

            _anchorScreenPoint = new Point(
                (_transformOrigin.X > 0.5) ? (targetLeft + popupW) : targetLeft,
                (_transformOrigin.Y > 0.5) ? (targetTop + popupH) : targetTop
            );

            PopupScale.ScaleX = 0.12;
            PopupScale.ScaleY = 0.12;
            RootContainer.Opacity = 0.0;

            // Capture screen area behind the popup for frosted glass / acrylic blur
            // Capture screen area behind the popup for frosted glass / acrylic blur
            try {
                double dpi = 1.0;
                try {
                    dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                } catch { }
                if (dpi <= 0) dpi = 1.0;

                double blurMargin = 25;
                double cardW = Math.Max(10, popupW - 20);
                double cardH = Math.Max(10, popupH - 20);
                double imgW = cardW + blurMargin * 2;
                double imgH = cardH + blurMargin * 2;

                CardClipGeometry.Rect = new Rect(0, 0, cardW, cardH);
                Canvas.SetLeft(PopupBlurImage, -blurMargin);
                Canvas.SetTop(PopupBlurImage, -blurMargin);
                PopupBlurImage.Width = imgW;
                PopupBlurImage.Height = imgH;

                double cardLeft = targetLeft + 10;
                double cardTop = targetTop + 10;
                double capLeft = Math.Max(screenLeft, cardLeft - blurMargin);
                double capTop = Math.Max(screenTop, cardTop - blurMargin);

                var screenBmp = ShellHelper.CaptureScreenArea(
                    (int)Math.Round(capLeft),
                    (int)Math.Round(capTop),
                    (int)Math.Round(imgW),
                    (int)Math.Round(imgH),
                    dpi);

                if (screenBmp != null) {
                    PopupBlurImage.Source = screenBmp;
                    PopupBlurImage.Visibility = Visibility.Visible;
                } else {
                    PopupBlurImage.Source = null;
                    PopupBlurImage.Visibility = Visibility.Collapsed;
                }
            } catch {
                PopupBlurImage.Source = null;
                PopupBlurImage.Visibility = Visibility.Collapsed;
            } finally {
                ApplyThemeBackground();
            }

            Show();
            Activate();
            Focus();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            ShellHelper.ForceForegroundWindow(hwnd);

            OpeningAnimationStarting?.Invoke();

            // Corner shutter expansion animation directly on top of the icon
            _isOpening = true;
            _openedTime = DateTime.UtcNow;
            int currentSession = _animationSessionId;
            Duration dur = new Duration(TimeSpan.FromMilliseconds(220));
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            DoubleAnimation animScaleX = new DoubleAnimation(0.12, 1.0, dur) { EasingFunction = easeOut };
            DoubleAnimation animScaleY = new DoubleAnimation(0.12, 1.0, dur) { EasingFunction = easeOut };
            DoubleAnimation animOpacity = new DoubleAnimation(0.0, 1.0, dur) { EasingFunction = easeOut };

            animScaleX.Completed += (s, e) => {
                if (_animationSessionId == currentSession) {
                    _isOpening = false;
                }
            };

            PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
            PopupTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            PopupTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            PopupTranslate.X = 0;
            PopupTranslate.Y = 0;
            RootContainer.BeginAnimation(OpacityProperty, animOpacity);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (_isOpening || _isClosing) {
                return;
            }
            if (e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0) {
                return;
            }

            double newW = e.NewSize.Width;
            double newH = e.NewSize.Height;

            double deltaW = Math.Abs(newW - e.PreviousSize.Width);
            double deltaH = Math.Abs(newH - e.PreviousSize.Height);
            if (deltaW < 0.5 && deltaH < 0.5) {
                return;
            }

            double screenLeft = SystemParameters.WorkArea.Left;
            double screenTop = SystemParameters.WorkArea.Top;
            double screenRight = SystemParameters.WorkArea.Right;
            double screenBottom = SystemParameters.WorkArea.Bottom;

            double newLeft;
            double newTop;

            if (SettingsService.Instance.Current.OpenInCenterOnly) {
                newLeft = screenLeft + (screenRight - screenLeft - newW) / 2.0;
                newTop = screenTop + (screenBottom - screenTop - newH) / 2.0;
            } else {
                newLeft = (_transformOrigin.X > 0.5) ? (_anchorScreenPoint.X - newW) : _anchorScreenPoint.X;
                newTop = (_transformOrigin.Y > 0.5) ? (_anchorScreenPoint.Y - newH) : _anchorScreenPoint.Y;

                // Clamp strictly within work area
                if (newLeft < screenLeft + 8) {
                    newLeft = screenLeft + 8;
                }
                if (newLeft + newW > screenRight - 8) {
                    newLeft = screenRight - newW - 8;
                }
                if (newTop < screenTop + 8) {
                    newTop = screenTop + 8;
                }
                if (newTop + newH > screenBottom - 8) {
                    newTop = screenBottom - newH - 8;
                }
            }

            if (Math.Abs(Left - newLeft) > 1) {
                Left = newLeft;
            }
            if (Math.Abs(Top - newTop) > 1) {
                Top = newTop;
            }

            double cardW = Math.Max(10, newW - 20);
            double cardH = Math.Max(10, newH - 20);
            CardClipGeometry.Rect = new Rect(0, 0, cardW, cardH);

            if (PopupBlurImage.Visibility == Visibility.Visible) {
                double blurMargin = 25;
                PopupBlurImage.Width = cardW + blurMargin * 2;
                PopupBlurImage.Height = cardH + blurMargin * 2;
            }
        }

        private void InnerClippingBorder_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0) {
                CardClipGeometry.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
        }

        private bool _isEditPanelOpen = false;

        private void CloseEditPanelImmediate() {
            _isEditPanelOpen = false;
            EditPanelContainer.BeginAnimation(HeightProperty, null);
            EditPanel.BeginAnimation(OpacityProperty, null);
            EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            EditPanelContainer.Height = 0;
            EditPanelContainer.Visibility = Visibility.Collapsed;
            EditPanel.Opacity = 0.0;
            EditPanelTranslate.Y = -10;
        }

        private void ToggleEditPanel(bool? forceOpen = null) {
            bool shouldOpen = forceOpen ?? !_isEditPanelOpen;
            if (_isEditPanelOpen == shouldOpen && EditPanelContainer.Visibility == (shouldOpen ? Visibility.Visible : Visibility.Collapsed)) {
                return;
            }

            _isEditPanelOpen = shouldOpen;

            Duration duration = new Duration(TimeSpan.FromMilliseconds(220));

            if (shouldOpen) {
                EditPanelContainer.Visibility = Visibility.Visible;

                // Stop any running animations
                EditPanelContainer.BeginAnimation(HeightProperty, null);
                EditPanel.BeginAnimation(OpacityProperty, null);
                EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);

                // Measure natural height of EditPanel (fixed 3-column width)
                double availableWidth = 234;
                EditPanel.Measure(new Size(availableWidth, double.PositiveInfinity));
                double targetHeight = EditPanel.DesiredSize.Height;
                if (targetHeight <= 0) targetHeight = 150;

                double currentHeight = EditPanelContainer.ActualHeight;
                if (currentHeight < 0 || double.IsNaN(currentHeight)) currentHeight = 0;

                var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
                var heightAnim = new DoubleAnimation(currentHeight, targetHeight, duration) { EasingFunction = easeOut };
                var opacityAnim = new DoubleAnimation(EditPanel.Opacity, 1.0, duration) { EasingFunction = easeOut };
                var translateAnim = new DoubleAnimation(EditPanelTranslate.Y, 0.0, duration) { EasingFunction = easeOut };

                heightAnim.Completed += (s, e) => {
                    if (_isEditPanelOpen) {
                        EditPanelContainer.BeginAnimation(HeightProperty, null);
                        EditPanelContainer.Height = double.NaN; // Auto-size
                        NameEditBox.Focus();
                        NameEditBox.SelectAll();
                    }
                };

                EditPanelContainer.BeginAnimation(HeightProperty, heightAnim);
                EditPanel.BeginAnimation(OpacityProperty, opacityAnim);
                EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
            } else {
                double startHeight = EditPanelContainer.ActualHeight;
                if (startHeight <= 0) {
                    CloseEditPanelImmediate();
                    return;
                }

                // Stop any running animations
                EditPanelContainer.BeginAnimation(HeightProperty, null);
                EditPanel.BeginAnimation(OpacityProperty, null);
                EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);

                var easeIn = new CubicEase { EasingMode = EasingMode.EaseInOut };
                var heightAnim = new DoubleAnimation(startHeight, 0.0, duration) { EasingFunction = easeIn };
                var opacityAnim = new DoubleAnimation(EditPanel.Opacity, 0.0, duration) { EasingFunction = easeIn };
                var translateAnim = new DoubleAnimation(EditPanelTranslate.Y, -10.0, duration) { EasingFunction = easeIn };

                heightAnim.Completed += (s, e) => {
                    if (!_isEditPanelOpen) {
                        EditPanelContainer.BeginAnimation(HeightProperty, null);
                        EditPanelContainer.Height = 0;
                        EditPanelContainer.Visibility = Visibility.Collapsed;
                    }
                };

                EditPanelContainer.BeginAnimation(HeightProperty, heightAnim);
                EditPanel.BeginAnimation(OpacityProperty, opacityAnim);
                EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
            }
        }

        public void CloseAnimated() {
            if (_isClosing) {
                return;
            }
            _isClosing = true;
            _isOpening = false;
            FlushPendingSettingsSave();
            int currentSession = ++_animationSessionId;

            Duration dur = new Duration(TimeSpan.FromMilliseconds(180));
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            DoubleAnimation animScaleX = new DoubleAnimation(0.12, dur) { EasingFunction = easeIn };
            DoubleAnimation animScaleY = new DoubleAnimation(0.12, dur) { EasingFunction = easeIn };
            DoubleAnimation animOpacity = new DoubleAnimation(0.0, dur) { EasingFunction = easeIn };

            animOpacity.Completed += (s, e) => {
                if (_animationSessionId == currentSession) {
                    Hide();
                    PopupBlurImage.Source = null;
                    ClosedAnimated?.Invoke();
                    PanakManager.Instance.CheckAndDissolveFolder(_rootFolderPath);
                }
            };

            RootContainer.RenderTransformOrigin = _transformOrigin;
            PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
            RootContainer.BeginAnimation(OpacityProperty, animOpacity);
        }

        // Header Actions
        private void EditButton_Click(object sender, RoutedEventArgs e) {
            ToggleEditPanel();
        }

        private void ExplorerButton_Click(object sender, RoutedEventArgs e) {
            ShellContextMenu.OpenFile(_currentFolderPath);
            CloseAnimated();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            CloseAnimated();
        }

        private void AlwaysPanakCheckBox_Checked(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            _folderData.AutoPopup = true;
            SaveMetadata();
            PanakManager.Instance.WatchFolder(_rootFolderPath);
            PanakManager.Instance.ScheduleFolderIconUpdate(_rootFolderPath, immediate: true);
        }

        private void AlwaysPanakCheckBox_Unchecked(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            _folderData.AutoPopup = false;
            SaveMetadata();
            PanakManager.Instance.RevertToRegularFolder(_rootFolderPath);
        }

        // Inline Title Rename via Double-click
        private void FolderTitleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                StartInlineRename();
                e.Handled = true;
            }
        }

        private void StartInlineRename() {
            InlineNameEditBox.Text = FolderTitleText.Text;
            FolderTitleText.Visibility = Visibility.Collapsed;
            InlineNameEditBox.Visibility = Visibility.Visible;
            InlineNameEditBox.Focus();
            InlineNameEditBox.SelectAll();
        }

        private bool RenameFolder(string newName) {
            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                return false;
            }

            if (string.Equals(newName, _folderData.Title, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string? parentDir = Path.GetDirectoryName(_rootFolderPath);
            if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) {
                return false;
            }

            string newPath = Path.Combine(parentDir, newName);
            if (string.Equals(_rootFolderPath, newPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (Directory.Exists(newPath)) {
                return false;
            }

            string oldPath = _rootFolderPath;
            try {
                PanakManager.Instance.UnwatchFolder(oldPath);
                Directory.Move(oldPath, newPath);

                _rootFolderPath = newPath;
                _currentFolderPath = newPath;
                _folderData.Title = newName;
                _folderData.Save(newPath);

                FolderTitleText.Text = newName;
                NameEditBox.Text = newName;
                InlineNameEditBox.Text = newName;

                PanakManager.Instance.RenameFolder(oldPath, newPath);
                PanakManager.Instance.ConvertToPanakFolder(newPath);
                PanakManager.Instance.ScheduleFolderIconUpdate(newPath, immediate: true);
                ShellHelper.NotifyFolderRenamed(oldPath, newPath);
                UpdateBreadcrumbs();
                return true;
            } catch {
                return false;
            }
        }

        private System.Threading.Timer? _externalRefreshDebounceTimer;
        private readonly object _externalRefreshLock = new object();

        public void OnExternalFolderContentsChanged(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) return;
            if (!string.Equals(_rootFolderPath, folderPath, StringComparison.OrdinalIgnoreCase) &&
                !_currentFolderPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            lock (_externalRefreshLock) {
                if (_externalRefreshDebounceTimer != null) {
                    try { _externalRefreshDebounceTimer.Change(100, System.Threading.Timeout.Infinite); } catch { }
                } else {
                    _externalRefreshDebounceTimer = new System.Threading.Timer(_ => {
                        lock (_externalRefreshLock) {
                            try { _externalRefreshDebounceTimer?.Dispose(); } catch { }
                            _externalRefreshDebounceTimer = null;
                        }

                        Dispatcher.BeginInvoke(() => {
                            if (IsVisible && !_isClosing) {
                                if (PanakManager.Instance.CheckAndDissolveFolder(_rootFolderPath)) {
                                    return;
                                }
                                RefreshItemsList();
                            }
                        });
                    }, null, 100, System.Threading.Timeout.Infinite);
                }
            }
        }

        public void OnExternalFolderRenamed(string oldPath, string newPath) {
            if (!string.Equals(_rootFolderPath, oldPath, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            string newName = Path.GetFileName(newPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            _rootFolderPath = newPath;
            if (_currentFolderPath.StartsWith(oldPath, StringComparison.OrdinalIgnoreCase)) {
                _currentFolderPath = newPath + _currentFolderPath.Substring(oldPath.Length);
            } else {
                _currentFolderPath = newPath;
            }

            _folderData.Title = newName;
            _folderData.Save(newPath);

            FolderTitleText.Text = newName;
            NameEditBox.Text = newName;
            InlineNameEditBox.Text = newName;

            UpdateBreadcrumbs();
            RefreshItemsList();
        }

        private void SaveInlineRename() {
            if (InlineNameEditBox.Visibility != Visibility.Visible) {
                return;
            }
            string newName = InlineNameEditBox.Text.Trim();
            InlineNameEditBox.Visibility = Visibility.Collapsed;
            FolderTitleText.Visibility = Visibility.Visible;

            if (!string.IsNullOrEmpty(newName) && !newName.Equals(_folderData.Title)) {
                RenameFolder(newName);
            }
        }

        private void InlineNameEditBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SaveInlineRename();
                e.Handled = true;
            } else if (e.Key == Key.Escape) {
                InlineNameEditBox.Visibility = Visibility.Collapsed;
                FolderTitleText.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        }

        private void InlineNameEditBox_LostFocus(object sender, RoutedEventArgs e) {
            SaveInlineRename();
        }

        private void SaveNameButton_Click(object sender, RoutedEventArgs e) {
            SaveNewName();
        }

        private void NameEditBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SaveNewName();
            } else if (e.Key == Key.Escape) {
                ToggleEditPanel(forceOpen: false);
            }
        }

        private void SaveNewName() {
            if (_isInitializing) {
                return;
            }
            string newName = NameEditBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName) && !newName.Equals(_folderData.Title)) {
                RenameFolder(newName);
            }
            ToggleEditPanel(forceOpen: false);
        }

        private System.Threading.Timer? _debouncedSettingsSaveTimer;
        private readonly object _debouncedSaveLock = new object();

        private void ScheduleDebouncedFolderSettingsSave(bool immediate) {
            string folderPath = _rootFolderPath;
            if (string.IsNullOrEmpty(folderPath)) return;

            if (immediate) {
                lock (_debouncedSaveLock) {
                    if (_debouncedSettingsSaveTimer != null) {
                        try { _debouncedSettingsSaveTimer.Dispose(); } catch { }
                        _debouncedSettingsSaveTimer = null;
                    }
                }

                try {
                    _folderData.Save(folderPath);
                    PanakManager.Instance.ScheduleFolderIconUpdate(folderPath, immediate: true, shape: _folderData.IconShape, color: _folderData.Color);
                } catch { }
                return;
            }

            int delayMs = 250;
            lock (_debouncedSaveLock) {
                if (_debouncedSettingsSaveTimer != null) {
                    try {
                        _debouncedSettingsSaveTimer.Change(delayMs, System.Threading.Timeout.Infinite);
                    } catch { }
                } else {
                    _debouncedSettingsSaveTimer = new System.Threading.Timer(_ => {
                        lock (_debouncedSaveLock) {
                            try {
                                _debouncedSettingsSaveTimer?.Dispose();
                            } catch { }
                            _debouncedSettingsSaveTimer = null;
                        }

                        try {
                            _folderData.Save(folderPath);
                            PanakManager.Instance.ScheduleFolderIconUpdate(folderPath, immediate: true, shape: _folderData.IconShape, color: _folderData.Color);
                        } catch { }
                    }, null, delayMs, System.Threading.Timeout.Infinite);
                }
            }
        }

        private void FlushPendingSettingsSave() {
            string folderPath = _rootFolderPath;
            if (string.IsNullOrEmpty(folderPath)) return;

            lock (_debouncedSaveLock) {
                if (_debouncedSettingsSaveTimer != null) {
                    try {
                        _debouncedSettingsSaveTimer.Dispose();
                    } catch { }
                    _debouncedSettingsSaveTimer = null;

                    try {
                        _folderData.Save(folderPath);
                        PanakManager.Instance.ScheduleFolderIconUpdate(folderPath, immediate: true, shape: _folderData.IconShape, color: _folderData.Color);
                    } catch { }
                }
            }
        }

        private void ColorSwatch_Checked(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            if (sender is RadioButton rb && rb.Tag is string hex) {
                _folderData.Color = hex;
                ApplyThemeColor(hex);

                _isInitializing = true;
                try {
                    HueSlider.Value = ShellHelper.GetHueFromHex(hex);
                    _folderData.AutoPopup = true;
                    AlwaysPanakCheckBox.IsChecked = true;
                } finally {
                    _isInitializing = false;
                }

                PanakManager.Instance.WatchFolder(_rootFolderPath);
                ScheduleDebouncedFolderSettingsSave(immediate: true);
            }
        }

        private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing) {
                return;
            }
            string pastelHex = ShellHelper.ColorFromPastelHueHex(e.NewValue);
            _folderData.Color = pastelHex;
            ApplyThemeColor(pastelHex);

            // Synchronize swatch selection visually without firing checked events
            _isInitializing = true;
            try {
                bool matched = false;
                foreach (var child in ColorSwatchesPanel.Children) {
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
                    foreach (var child in ColorSwatchesPanel.Children) {
                        if (child is RadioButton rb && rb.IsChecked == true) {
                            rb.IsChecked = false;
                        }
                    }
                }
                _folderData.AutoPopup = true;
                AlwaysPanakCheckBox.IsChecked = true;
            } finally {
                _isInitializing = false;
            }

            PanakManager.Instance.WatchFolder(_rootFolderPath);
            ScheduleDebouncedFolderSettingsSave(immediate: false);
        }

        private void ApplyOpacity(double opacity) {
            opacity = Math.Clamp(opacity, 0.20, 1.0);
            FrostedTintLayer.Opacity = opacity;
            TintLayer.Opacity = Math.Min(1.0, opacity * 1.1);
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing) {
                return;
            }
            ApplyOpacity(e.NewValue);
            _folderData.BackgroundOpacity = e.NewValue;
            PanakManager.Instance.WatchFolder(_rootFolderPath);
            ScheduleDebouncedFolderSettingsSave(immediate: false);
        }

        private void ApplyThemeBackground() {
            bool hasBlur = PopupBlurImage.Visibility == Visibility.Visible && PopupBlurImage.Source != null;
            if (_isCurrentThemeDark) {
                FrostedTintLayer.Background = new SolidColorBrush(hasBlur
                    ? System.Windows.Media.Color.FromArgb(194, 22, 22, 29)
                    : System.Windows.Media.Color.FromArgb(235, 24, 24, 30));
            } else {
                FrostedTintLayer.Background = new SolidColorBrush(hasBlur
                    ? System.Windows.Media.Color.FromArgb(220, 248, 249, 250)
                    : System.Windows.Media.Color.FromArgb(245, 248, 249, 250));
            }
        }

        public void ApplyThemeMode(string mode) {
            try {
                bool isDark = mode == "Dark" || (mode != "Light" && ShellHelper.IsWindowsDarkTheme());
                _isCurrentThemeDark = isDark;

                ApplyThemeBackground();

                var textColor = isDark ? System.Windows.Media.Color.FromRgb(248, 250, 252) : System.Windows.Media.Color.FromRgb(15, 23, 42);
                var subtextColor = isDark ? System.Windows.Media.Color.FromRgb(148, 163, 184) : System.Windows.Media.Color.FromRgb(100, 116, 139);

                if (Resources["PopupTextBrush"] is SolidColorBrush ptb && !ptb.IsFrozen) {
                    ptb.Color = textColor;
                } else {
                    Resources["PopupTextBrush"] = new SolidColorBrush(textColor);
                }
                if (Resources["PopupSubtextBrush"] is SolidColorBrush psb && !psb.IsFrozen) {
                    psb.Color = subtextColor;
                } else {
                    Resources["PopupSubtextBrush"] = new SolidColorBrush(subtextColor);
                }

                if (FolderItemsControl != null) {
                    FolderItemsControl.Foreground = new SolidColorBrush(textColor);
                }

                if (isDark) {
                    // Dark Theme
                    CardBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(56, 255, 255, 255));
                    CardBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));

                    FolderTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));
                    EmptyTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    EmptySubtext.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
                    EditNameLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
                    ColorThemeLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
                    AlwaysPanakCheckBox.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));

                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupHeaderBtnFg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupItemBadgeFg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 255, 255, 255));
                } else {
                    // Light Theme
                    CardBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0));
                    CardBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255));

                    FolderTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
                    EmptyTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
                    EmptySubtext.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
                    EditNameLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
                    ColorThemeLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
                    AlwaysPanakCheckBox.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));

                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 0, 0, 0));
                    Resources["PopupHeaderBtnFg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                    Resources["PopupItemBadgeFg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                }
            } catch {
                // Ignore theme application errors
            }
        }


        private void ShapeRadio_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            string chosenShape = "Squircle";
            if (sender == ShapeRadioCircle) chosenShape = "Circle";
            else if (sender == ShapeRadioSquircle) chosenShape = "Squircle";
            else if (sender == ShapeRadioClover) chosenShape = "Clover";
            else if (sender == ShapeRadioScallop) chosenShape = "Scallop";
            else if (sender == ShapeRadioArch) chosenShape = "Arch";

            if (!string.Equals(_folderData.IconShape, chosenShape, StringComparison.OrdinalIgnoreCase)) {
                _folderData.IconShape = chosenShape;
                _isInitializing = true;
                try {
                    _folderData.AutoPopup = true;
                    AlwaysPanakCheckBox.IsChecked = true;
                } finally {
                    _isInitializing = false;
                }

                PanakManager.Instance.WatchFolder(_rootFolderPath);
                ScheduleDebouncedFolderSettingsSave(immediate: true);
            }
        }

        // Item interactions
        private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            _dragStartPoint = e.GetPosition(this);
            if (e.ClickCount == 2 && sender is FrameworkElement elem && elem.DataContext is PanakItem item) {
                if (item.IsDirectory) {
                    NavigateTo(item.FullPath);
                } else {
                    ShellContextMenu.OpenFile(item.FullPath);
                    CloseAnimated();
                }
            }
        }

        private void Item_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            // Context menu opens automatically on right click
        }

        private void Item_MouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed && !SettingsService.Instance.Current.IsLocked) {
                Point currentPoint = e.GetPosition(this);
                Vector diff = _dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) {
                    if (sender is FrameworkElement elem && elem.DataContext is PanakItem item) {
                        string[] files = new[] { item.FullPath };
                        DataObject data = new DataObject(DataFormats.FileDrop, files);
                        DragDropEffects effect = DragDrop.DoDragDrop(elem, data, DragDropEffects.Copy | DragDropEffects.Move);

                        // If files were moved or deleted out of the folder, immediately check dissolution or refresh
                        if (PanakManager.Instance.CheckAndDissolveFolder(_rootFolderPath)) {
                            return;
                        }
                        RefreshItemsList();
                        if (PanakManager.Instance.IsPanakFolder(_rootFolderPath) || AlwaysPanakCheckBox.IsChecked == true || File.Exists(Path.Combine(_rootFolderPath, "desktop.ini"))) {
                            PanakManager.Instance.ScheduleFolderIconUpdate(_rootFolderPath, immediate: true);
                        }
                    }
                }
            }
        }

        protected override void OnDragEnter(DragEventArgs e) {
            base.OnDragEnter(e);
            UpdateDragEffects(e);
        }

        protected override void OnDragOver(DragEventArgs e) {
            base.OnDragOver(e);
            UpdateDragEffects(e);
        }

        private void UpdateDragEffects(DragEventArgs e) {
            if (SettingsService.Instance.Current.IsLocked) {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey) {
                    e.Effects = DragDropEffects.Copy;
                } else {
                    e.Effects = DragDropEffects.Move;
                }
                e.Handled = true;
            }
        }

        protected override void OnDrop(DragEventArgs e) {
            base.OnDrop(e);

            if (SettingsService.Instance.Current.IsLocked) {
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[]? droppedFiles = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (droppedFiles != null && droppedFiles.Length > 0) {
                    bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;

                    foreach (var file in droppedFiles) {
                        try {
                            string fileName = Path.GetFileName(file);
                            if (string.IsNullOrEmpty(fileName)) {
                                continue;
                            }

                            string targetPath = Path.Combine(_currentFolderPath, fileName);
                            if (string.Equals(file, targetPath, StringComparison.OrdinalIgnoreCase)) {
                                continue;
                            }

                            if (File.Exists(file)) {
                                if (isCopy) {
                                    File.Copy(file, targetPath, true);
                                } else {
                                    if (File.Exists(targetPath)) {
                                        File.Delete(targetPath);
                                    }
                                    try {
                                        File.Move(file, targetPath, true);
                                    } catch {
                                        File.Copy(file, targetPath, true);
                                        File.Delete(file);
                                    }
                                    ShellHelper.NotifyItemDeleted(file, false);
                                }
                            } else if (Directory.Exists(file) && !file.Equals(_currentFolderPath, StringComparison.OrdinalIgnoreCase)) {
                                if (isCopy) {
                                    CopyDirectory(file, targetPath);
                                } else {
                                    if (Directory.Exists(targetPath)) {
                                        Directory.Delete(targetPath, true);
                                    }
                                    try {
                                        Directory.Move(file, targetPath);
                                    } catch {
                                        CopyDirectory(file, targetPath);
                                        Directory.Delete(file, true);
                                    }
                                    ShellHelper.NotifyItemDeleted(file, true);
                                }
                            }
                        } catch {
                            // File access error
                        }
                    }

                    RefreshItemsList();
                    if (PanakManager.Instance.IsPanakFolder(_rootFolderPath) || AlwaysPanakCheckBox.IsChecked == true) {
                        PanakManager.Instance.ScheduleFolderIconUpdate(_rootFolderPath, immediate: true);
                    }
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir) {
            var dir = new DirectoryInfo(sourceDir);
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles()) {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories()) {
                string newDestDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestDir);
            }
        }

        // Context Menu actions on files inside popup
        private void MenuItemOpen_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi && mi.DataContext is PanakItem item) {
                ShellContextMenu.OpenFile(item.FullPath);
                CloseAnimated();
            }
        }

        private void MenuItemOpenInExplorer_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi && mi.DataContext is PanakItem item) {
                ShellContextMenu.ShowInExplorer(item.FullPath);
                CloseAnimated();
            }
        }

        private void MenuItemRemove_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi && mi.DataContext is PanakItem item) {
                try {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string target = Path.Combine(desktop, item.DisplayName);
                    if (File.Exists(item.FullPath)) {
                        File.Move(item.FullPath, target, true);
                    } else if (Directory.Exists(item.FullPath)) {
                        Directory.Move(item.FullPath, target);
                    }
                    ShellHelper.NotifyShellItemChanged(target);
                    if (PanakManager.Instance.CheckAndDissolveFolder(_rootFolderPath)) {
                        return;
                    }
                    RefreshItemsList();
                    if (PanakManager.Instance.IsPanakFolder(_rootFolderPath) || AlwaysPanakCheckBox.IsChecked == true || File.Exists(Path.Combine(_rootFolderPath, "desktop.ini"))) {
                        PanakManager.Instance.ScheduleFolderIconUpdate(_rootFolderPath, immediate: true);
                    }
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Panak", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void MenuItemDelete_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi && mi.DataContext is PanakItem item) {
                ShellContextMenu.DeleteFile(item.FullPath);
                if (PanakManager.Instance.CheckAndDissolveFolder(_rootFolderPath)) {
                    return;
                }
                RefreshItemsList();
                if (PanakManager.Instance.IsPanakFolder(_rootFolderPath) || AlwaysPanakCheckBox.IsChecked == true || File.Exists(Path.Combine(_rootFolderPath, "desktop.ini"))) {
                    PanakManager.Instance.ScheduleFolderIconUpdate(_rootFolderPath, immediate: true);
                }
            }
        }

        private void MenuItemProperties_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi && mi.DataContext is PanakItem item) {
                ShellContextMenu.ShowProperties(item.FullPath);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                if (InlineNameEditBox.Visibility == Visibility.Visible) {
                    InlineNameEditBox.Visibility = Visibility.Collapsed;
                    FolderTitleText.Visibility = Visibility.Visible;
                    e.Handled = true;
                    return;
                }
                CloseAnimated();
                e.Handled = true;
            } else if (e.Key == Key.Back && InlineNameEditBox.Visibility != Visibility.Visible && !NameEditBox.IsFocused) {
                if (!string.Equals(_currentFolderPath, _rootFolderPath, StringComparison.OrdinalIgnoreCase)) {
                    NavigateUp();
                    e.Handled = true;
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) {
                    return typedChild;
                }
                var subChild = FindVisualChild<T>(child);
                if (subChild != null) {
                    return subChild;
                }
            }
            return null;
        }
    }
}
