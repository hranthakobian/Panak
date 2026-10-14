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
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;
using Polyline = System.Windows.Shapes.Polyline;
using Panak.Models;
using Panak.Services;

namespace Panak.Views {
    public partial class FolderPopupWindow : Window {
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_WINDOWPOSCHANGED = 0x0047;
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
        public const double ItemCellWidth = 78.0;
        public const double ItemCellHeight = 86.0;

        private bool _isInitializing = false;
        private DateTime _openedTime = DateTime.MinValue;
        private int _animationSessionId = 0;
        private UniformGrid? _itemsUniformGrid;
        private bool _isCurrentThemeDark = true;
        private bool _isPinned = false;
        private bool _isCustomPositioned = false;

        public bool IsPinned => _isPinned;
        public string RootFolderPath => _rootFolderPath;
        public string CurrentFolderPath => _currentFolderPath;

        public bool IsWithinOpenGracePeriod() {
            return _isOpening || (DateTime.UtcNow - _openedTime).TotalMilliseconds < 750;
        }

        public ObservableCollection<PanakItem> Items { get; } = new ObservableCollection<PanakItem>();
        public event Action? ClosedAnimated;
        public event Action? OpeningAnimationStarting;

        public FolderPopupWindow() {
            InitializeComponent();
            FolderItemsControl.ItemsSource = Items;
            FolderItemsControl.Loaded += (s, e) => ApplySettings();
            ApplyLocalization();

            LocationChanged += (s, e) => {
                if (!_isOpening && !_isClosing && IsVisible) {
                    UpdatePopupBlurBehind(false);
                }
            };

            SettingsService.Instance.SettingsChanged += OnSettingsChanged;
            Closed += (s, e) => {
                SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
            };

            PopupLockOverlay.Unlocked += () => {
                UpdateLockState();
            };
            PopupLockOverlay.LockRemoved += () => {
                UpdateLockState();
            };
            PopupLockOverlay.Cancelled += () => {
                UpdateLockState();
            };
        }

        private void OnSettingsChanged(AppSettings s) {
            try {
                if (Dispatcher.Thread.IsAlive && !Dispatcher.HasShutdownStarted) {
                    if (Dispatcher.CheckAccess()) {
                        ApplyThemeMode(s.ThemeMode ?? "Auto");
                        if (_itemsUniformGrid != null) {
                            _itemsUniformGrid.Columns = GetConfiguredColumns();
                        }
                    } else {
                        Dispatcher.BeginInvoke(() => {
                            try {
                                ApplyThemeMode(s.ThemeMode ?? "Auto");
                                if (_itemsUniformGrid != null) {
                                    _itemsUniformGrid.Columns = GetConfiguredColumns();
                                }
                            } catch { }
                        });
                    }
                }
            } catch { }
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
                if (!_isCustomPositioned) {
                    var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    if ((pos.flags & SWP_NOSIZE) == 0 && pos.cx > 0 && pos.cy > 0) {
                        var dpi = VisualTreeHelper.GetDpi(this);
                        double dpiX = dpi.DpiScaleX;
                        double dpiY = dpi.DpiScaleY;

                        var workArea = ShellHelper.GetMonitorWorkArea(hwnd);
                        if (workArea.IsEmpty || workArea.Width <= 0) {
                            workArea = SystemParameters.WorkArea;
                        }
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
            } else if (msg == WM_WINDOWPOSCHANGED && !_isOpening && !_isClosing && IsVisible && _isCustomPositioned) {
                try {
                    var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    if ((pos.flags & SWP_NOMOVE) == 0) {
                        var dpi = VisualTreeHelper.GetDpi(this);
                        double curLeft = pos.x / dpi.DpiScaleX;
                        double curTop = pos.y / dpi.DpiScaleY;
                        UpdatePopupBlurBehind(false, curLeft, curTop);
                    }
                } catch { }
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

            UpdateDisplayAffinity();
        }

        public void UpdateDisplayAffinity() {
            try {
                if (ShellHelper.IsWindows11OrGreater) {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero) {
                        bool isLocked = !string.IsNullOrEmpty(_rootFolderPath) && FolderLockService.Instance.IsFolderLocked(_rootFolderPath);
                        uint affinity = isLocked ? ShellHelper.WDA_EXCLUDEFROMCAPTURE : ShellHelper.WDA_NONE;
                        ShellHelper.SetWindowDisplayAffinity(hwnd, affinity);
                    }
                }
            } catch { }
        }

        protected override void OnDeactivated(EventArgs e) {
            base.OnDeactivated(e);
            if (_isPinned) {
                return;
            }
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
            PinButton.ToolTip = _isPinned ? loc["Unpin"] : loc["Pin"];
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
            CloseEditPanelImmediate();
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
            _currentSliderPage = 0;
            PagesTranslateX.BeginAnimation(TranslateTransform.XProperty, null);
            PagesTranslateX.X = 0;

            LoadMetadata();
            RefreshItemsList();
            ApplySettings();

            _isInitializing = true;
            try {
                // Set Title & Count
                UpdateTitleMarquee(_folderData.Title);
                FolderTitleText.Visibility = Visibility.Visible;
                TitleMarqueeCanvas.Visibility = Visibility.Visible;
                InlineNameEditBox.Visibility = Visibility.Collapsed;
                UpdateItemCountText();
                UpdateBreadcrumbs();
                UpdatePinVisual();

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
                double defaultOp = SettingsService.Instance.Current.FolderOpacity;
                if (defaultOp <= 0) defaultOp = 0.85;
                double op = _folderData.BackgroundOpacity > 0 ? _folderData.BackgroundOpacity : defaultOp;
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
                bool isPanak = PanakManager.Instance.IsPanakFolder(_rootFolderPath);
                AlwaysPanakCheckBox.IsChecked = isPanak;
                _folderData.AutoPopup = isPanak;
                CloseEditPanelImmediate();
            } finally {
                _isInitializing = false;
            }
            UpdateLockState();
            EnsurePanakJsonSync();

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
            _currentSliderPage = 0;
            PagesTranslateX.BeginAnimation(TranslateTransform.XProperty, null);
            PagesTranslateX.X = 0;
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

        private int _currentSliderPage = 0;
        private int _totalSliderPages = 1;
        private bool _isDraggingPages = false;
        private double _dragStartX = 0;
        private double _dragStartTranslateX = 0;

        private void ApplySettings() {
            int maxCols = SettingsService.Instance.Current.GridColumns;
            if (maxCols < 3) maxCols = 3;
            if (maxCols > 8) maxCols = 8;
            int maxRows = Math.Clamp(SettingsService.Instance.Current.GridRows, 1, 5);

            int columns = (Items.Count > 0 && Items.Count < maxCols) ? Math.Max(1, Items.Count) : maxCols;
            bool isSlider = string.Equals(SettingsService.Instance.Current.PagingMode, "Slider", StringComparison.OrdinalIgnoreCase);

            if (isSlider) {
                ItemsScrollViewer.Visibility = Visibility.Collapsed;
                SliderPagesViewport.Visibility = Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                BuildSliderPages();
            } else {
                SliderPagesViewport.Visibility = Visibility.Collapsed;
                FooterPagerGrid.Visibility = Visibility.Collapsed;
                ItemsScrollViewer.Visibility = Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                FolderItemsControl.ItemsSource = Items;

                double itemW = ItemCellWidth;
                double itemH = ItemCellHeight;
                FolderItemsControl.Width = columns * itemW;
                ItemsScrollViewer.MaxHeight = maxRows * itemH + 8;

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
        }

        public void ApplySettingsFromGlobal() {
            ApplySettings();
        }

        private void BuildSliderPages() {
            if (Items.Count == 0) {
                SliderPagesPanel.Children.Clear();
                FooterPagerGrid.Visibility = Visibility.Collapsed;
                return;
            }

            int maxCols = SettingsService.Instance.Current.GridColumns;
            if (maxCols < 3) maxCols = 3;
            if (maxCols > 8) maxCols = 8;
            int maxRows = Math.Clamp(SettingsService.Instance.Current.GridRows, 1, 5);

            int cols = (Items.Count > 0 && Items.Count < maxCols) ? Math.Max(1, Items.Count) : maxCols;
            int rows = Math.Min(maxRows, Math.Max(1, (int)Math.Ceiling((double)Items.Count / cols)));
            int itemsPerPage = Math.Max(1, cols * rows);

            _totalSliderPages = Math.Max(1, (int)Math.Ceiling((double)Items.Count / itemsPerPage));
            if (_currentSliderPage >= _totalSliderPages) {
                _currentSliderPage = _totalSliderPages - 1;
            }
            if (_currentSliderPage < 0) {
                _currentSliderPage = 0;
            }

            double itemW = ItemCellWidth;
            double itemH = ItemCellHeight;
            double pageW = cols * itemW;
            double pageH = rows * itemH;

            SliderPagesViewport.Width = pageW;
            SliderPagesViewport.Height = pageH;
            SliderPagesPanel.Width = _totalSliderPages * pageW;
            SliderPagesPanel.Height = pageH;
            SliderPagesPanel.Children.Clear();
            var itemTemplate = (DataTemplate)Resources["PanakItemDataTemplate"];

            for (int p = 0; p < _totalSliderPages; p++) {
                var pageItems = Items.Skip(p * itemsPerPage).Take(itemsPerPage).ToList();

                var factory = new FrameworkElementFactory(typeof(UniformGrid));
                factory.SetValue(UniformGrid.ColumnsProperty, cols);
                factory.SetValue(UniformGrid.RowsProperty, rows);
                factory.SetValue(FrameworkElement.WidthProperty, pageW);
                factory.SetValue(FrameworkElement.HeightProperty, pageH);

                var itemsCtrl = new ItemsControl {
                    ItemsSource = pageItems,
                    ItemTemplate = itemTemplate,
                    ItemsPanel = new ItemsPanelTemplate(factory),
                    Width = pageW,
                    Height = pageH,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                SliderPagesPanel.Children.Add(itemsCtrl);
            }

            UpdateSliderDots();
            NavigateToSliderPage(_currentSliderPage);
        }

        private void UpdateSliderDots() {
            SliderDotsPanel.Children.Clear();
            if (_totalSliderPages <= 1) {
                FooterPagerGrid.Visibility = Visibility.Collapsed;
                return;
            }

            int visibleDots = Math.Min(5, _totalSliderPages);
            int startIndex = (_totalSliderPages > 5)
                ? Math.Clamp(_currentSliderPage - 2, 0, _totalSliderPages - 5)
                : 0;

            var textBrush = TryFindResource("PopupTextBrush") as Brush ?? Brushes.White;
            var subtextBrush = TryFindResource("PopupSubtextBrush") as Brush ?? Brushes.Gray;

            for (int k = 0; k < visibleDots; k++) {
                int pageIdx = startIndex + k;
                bool isActive = (pageIdx == _currentSliderPage);

                var dot = new Border {
                    Width = isActive ? 20.0 : 7.0,
                    Height = 7.0,
                    CornerRadius = new CornerRadius(3.5),
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    Background = isActive ? textBrush : subtextBrush,
                    Opacity = isActive ? 0.95 : 0.45,
                    Cursor = Cursors.Hand,
                    Tag = pageIdx
                };
                dot.MouseLeftButtonDown += (s, e) => {
                    if (s is FrameworkElement fe && fe.Tag is int targetPage) {
                        NavigateToSliderPage(targetPage);
                    }
                };
                SliderDotsPanel.Children.Add(dot);
            }

            bool arrowsEnabled = SettingsService.Instance.Current.SliderArrowsEnabled;
            SliderPrevButton.Visibility = arrowsEnabled ? Visibility.Visible : Visibility.Collapsed;
            SliderNextButton.Visibility = arrowsEnabled ? Visibility.Visible : Visibility.Collapsed;

            SliderPrevButton.IsEnabled = _currentSliderPage > 0;
            SliderNextButton.IsEnabled = _currentSliderPage < _totalSliderPages - 1;
            SliderPrevButton.Opacity = _currentSliderPage > 0 ? 1.0 : 0.35;
            SliderNextButton.Opacity = _currentSliderPage < _totalSliderPages - 1 ? 1.0 : 0.35;

            FooterPagerGrid.Visibility = Visibility.Visible;
        }

        private void NavigateToSliderPage(int targetPage) {
            if (_totalSliderPages <= 0) return;
            targetPage = Math.Clamp(targetPage, 0, _totalSliderPages - 1);
            _currentSliderPage = targetPage;

            double pageWidth = SliderPagesViewport.Width > 0
                ? SliderPagesViewport.Width
                : (SliderPagesViewport.ActualWidth > 0 ? SliderPagesViewport.ActualWidth : 280);

            double targetX = -targetPage * pageWidth;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var anim = new DoubleAnimation {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = ease
            };
            PagesTranslateX.BeginAnimation(TranslateTransform.XProperty, anim);

            UpdateSliderDots();
        }

        private void SliderViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!string.Equals(SettingsService.Instance.Current.PagingMode, "Slider", StringComparison.OrdinalIgnoreCase) || _totalSliderPages <= 1) {
                return;
            }
            if (e.Delta < 0) {
                if (_currentSliderPage < _totalSliderPages - 1) {
                    NavigateToSliderPage(_currentSliderPage + 1);
                    e.Handled = true;
                }
            } else if (e.Delta > 0) {
                if (_currentSliderPage > 0) {
                    NavigateToSliderPage(_currentSliderPage - 1);
                    e.Handled = true;
                }
            }
        }

        private void SliderViewport_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (!SettingsService.Instance.Current.SliderDragEnabled) return;
            if (e.LeftButton == MouseButtonState.Pressed && _totalSliderPages > 1) {
                _dragStartX = e.GetPosition(SliderPagesViewport).X;
                _dragStartTranslateX = PagesTranslateX.Value.OffsetX;
                _isDraggingPages = true;
            }
        }

        private void SliderViewport_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (!_isDraggingPages || !SettingsService.Instance.Current.SliderDragEnabled) return;
            if (e.LeftButton == MouseButtonState.Pressed) {
                double curX = e.GetPosition(SliderPagesViewport).X;
                double delta = curX - _dragStartX;
                if (Math.Abs(delta) > 8) {
                    SliderPagesViewport.CaptureMouse();
                    double pageWidth = SliderPagesViewport.ActualWidth > 0 ? SliderPagesViewport.ActualWidth : 350;
                    double minX = -(_totalSliderPages - 1) * pageWidth;
                    double maxX = 0;
                    double proposedX = _dragStartTranslateX + delta;
                    if (proposedX > maxX) proposedX = maxX + (proposedX - maxX) * 0.3;
                    if (proposedX < minX) proposedX = minX + (proposedX - minX) * 0.3;
                    PagesTranslateX.BeginAnimation(TranslateTransform.XProperty, null);
                    PagesTranslateX.X = proposedX;
                }
            } else {
                _isDraggingPages = false;
                SliderPagesViewport.ReleaseMouseCapture();
            }
        }

        private void SliderViewport_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            if (!_isDraggingPages) return;
            _isDraggingPages = false;
            SliderPagesViewport.ReleaseMouseCapture();

            double curX = e.GetPosition(SliderPagesViewport).X;
            double delta = curX - _dragStartX;

            if (delta < -35 && _currentSliderPage < _totalSliderPages - 1) {
                NavigateToSliderPage(_currentSliderPage + 1);
            } else if (delta > 35 && _currentSliderPage > 0) {
                NavigateToSliderPage(_currentSliderPage - 1);
            } else {
                NavigateToSliderPage(_currentSliderPage);
            }
        }

        private void SliderPrevButton_Click(object sender, RoutedEventArgs e) {
            if (_currentSliderPage > 0) {
                NavigateToSliderPage(_currentSliderPage - 1);
            }
        }

        private void SliderNextButton_Click(object sender, RoutedEventArgs e) {
            if (_currentSliderPage < _totalSliderPages - 1) {
                NavigateToSliderPage(_currentSliderPage + 1);
            }
        }

        private void LoadMetadata() {
            _folderData = PanakFolderData.Load(_rootFolderPath);
        }

        private void SaveMetadata() {
            if (_isInitializing) {
                return;
            }
            if (AlwaysPanakCheckBox.IsChecked == true) {
                _folderData.AutoPopup = true;
                _folderData.Save(_rootFolderPath);
            } else {
                EnsurePanakJsonSync();
            }
        }

        public void EnsurePanakJsonSync() {
            if (string.IsNullOrEmpty(_rootFolderPath) || !Directory.Exists(_rootFolderPath)) {
                return;
            }
            string metaFile = Path.Combine(_rootFolderPath, "panak.json");
            bool isAlwaysPanak = AlwaysPanakCheckBox.IsChecked == true;

            if (isAlwaysPanak) {
                _folderData.AutoPopup = true;
                if (!File.Exists(metaFile)) {
                    _folderData.Save(_rootFolderPath);
                }
                PanakManager.Instance.WatchFolder(_rootFolderPath);
            } else {
                _folderData.AutoPopup = false;
                CancelPendingDebouncedSave();
                if (File.Exists(metaFile)) {
                    try {
                        File.SetAttributes(metaFile, FileAttributes.Normal);
                        File.Delete(metaFile);
                    } catch { }
                }
            }
        }

        public void RefreshItemsList() {
            double oldContentHeight = (IsLoaded && !_isOpening && !_isClosing && ContentAreaGrid.IsVisible)
                ? ContentAreaGrid.ActualHeight
                : -1;

            Items.Clear();

            if (!string.IsNullOrEmpty(_rootFolderPath) && FolderLockService.Instance.IsFolderLocked(_rootFolderPath)) {
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                ItemsScrollViewer.Visibility = Visibility.Collapsed;
                SliderPagesViewport.Visibility = Visibility.Collapsed;
                FooterPagerGrid.Visibility = Visibility.Collapsed;
                PopupLockOverlay.Visibility = Visibility.Visible;
                ExplorerButton.Visibility = Visibility.Collapsed;
                return;
            }

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
                                !f.Name.Equals("panak_lock.json", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                                !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .OrderBy(f => !f.Attributes.HasFlag(FileAttributes.Directory))
                    .ThenBy(f => f.Name)
                    .ToList();

                foreach (var entry in entries) {
                    bool isDir = entry.Attributes.HasFlag(FileAttributes.Directory);
                    string sizeText = isDir ? "Folder" : FormatFileSize(((FileInfo)entry).Length);
                    string displayName = entry.Name;
                    if (!isDir && string.Equals(entry.Extension, ".lnk", StringComparison.OrdinalIgnoreCase)) {
                        displayName = Path.GetFileNameWithoutExtension(entry.Name);
                    }

                    var item = new PanakItem {
                        FullPath = entry.FullName,
                        DisplayName = displayName,
                        Extension = entry.Extension,
                        IsDirectory = isDir,
                        IconSource = ShellHelper.GetFileThumbnailSource(entry.FullName, isLarge: true),
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

                // Dynamic header button background tinted with folder theme color
                var headerBtnBg = _isCurrentThemeDark
                    ? System.Windows.Media.Color.FromArgb(48, primaryColor.R, primaryColor.G, primaryColor.B)
                    : System.Windows.Media.Color.FromArgb(32, primaryColor.R, primaryColor.G, primaryColor.B);
                Resources["PopupHeaderBtnBg"] = new SolidColorBrush(headerBtnBg);

                if (PopupLockOverlay != null) {
                    PopupLockOverlay.ApplyFolderTheme(hexColor, _isCurrentThemeDark);
                }
            } catch {
                // Fallback
            }
        }

        private void PositionAndAnimateIn() {
            UpdateLayout();

            if (_iconScreenBounds.IsEmpty) {
                _iconScreenBounds = ExplorerHelper.GetBoundingBoxForPath(_currentFolderPath);
            }

            Point testPoint = !_iconScreenBounds.IsEmpty 
                ? new Point(_iconScreenBounds.Left, _iconScreenBounds.Top)
                : (ShellHelper.GetCursorPos(out var curPt) ? new Point(curPt.X, curPt.Y) : new Point(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top));

            Rect monitorWorkArea = ShellHelper.GetMonitorWorkArea(testPoint);
            double screenLeft = monitorWorkArea.Left;
            double screenTop = monitorWorkArea.Top;
            double screenRight = monitorWorkArea.Right;
            double screenBottom = monitorWorkArea.Bottom;

            RootContainer.Measure(new Size(screenRight - screenLeft - 16, screenBottom - screenTop - 16));
            double desiredW = RootContainer.DesiredSize.Width;
            double desiredH = RootContainer.DesiredSize.Height;

            double popupW = Math.Max(desiredW, ActualWidth > 0 ? ActualWidth : MinWidth);
            double popupH = Math.Max(desiredH, ActualHeight > 0 ? ActualHeight : MinHeight);
            popupW = Math.Clamp(popupW, MinWidth, MaxWidth);
            popupH = Math.Clamp(popupH, MinHeight, MaxHeight);

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

                // Available space from icon in all 4 directions
                double spaceRight = (screenRight - 8) - (iconX - 8);
                double spaceLeft = (_iconScreenBounds.Right + 8) - (screenLeft + 8);
                double spaceDown = (screenBottom - 8) - (iconY - 8);
                double spaceUp = (_iconScreenBounds.Bottom + 8) - (screenTop + 8);

                // If opening to the right would overflow the monitor, flip to opposite direction (open leftward)
                bool openLeft;
                if (spaceRight >= popupW) {
                    openLeft = false;
                } else if (spaceLeft >= popupW) {
                    openLeft = true;
                } else {
                    openLeft = spaceLeft > spaceRight;
                }

                // If opening downward would overflow the monitor, flip to opposite direction (open upward)
                bool openUp;
                if (spaceDown >= popupH) {
                    openUp = false;
                } else if (spaceUp >= popupH) {
                    openUp = true;
                } else {
                    openUp = spaceUp > spaceDown;
                }

                if (!openLeft && !openUp) {
                    // Top-Left anchor: opens downward and to the right
                    targetLeft = iconX - 8;
                    targetTop = iconY - 8;
                    originPoint = new Point(0.0, 0.0);
                } else if (openLeft && !openUp) {
                    // Top-Right anchor: opens downward and to the left
                    targetLeft = _iconScreenBounds.Right + 8 - popupW;
                    targetTop = iconY - 8;
                    originPoint = new Point(1.0, 0.0);
                } else if (!openLeft && openUp) {
                    // Bottom-Left anchor: opens upward and to the right
                    targetLeft = iconX - 8;
                    targetTop = _iconScreenBounds.Bottom + 8 - popupH;
                    originPoint = new Point(0.0, 1.0);
                } else {
                    // Bottom-Right anchor: opens upward and to the left
                    targetLeft = _iconScreenBounds.Right + 8 - popupW;
                    targetTop = _iconScreenBounds.Bottom + 8 - popupH;
                    originPoint = new Point(1.0, 1.0);
                }
            } else {
                if (ShellHelper.GetCursorPos(out var pt)) {
                    double cursorX = pt.X;
                    double cursorY = pt.Y;
                    double spaceRight = (screenRight - 8) - cursorX;
                    double spaceLeft = cursorX - (screenLeft + 8);
                    double spaceDown = (screenBottom - 8) - cursorY;
                    double spaceUp = cursorY - (screenTop + 8);

                    bool openLeft = (spaceRight < popupW && spaceLeft >= popupW) || (spaceRight < popupW && spaceLeft > spaceRight);
                    bool openUp = (spaceDown < popupH && spaceUp >= popupH) || (spaceDown < popupH && spaceUp > spaceDown);

                    targetLeft = openLeft ? cursorX - popupW : cursorX;
                    targetTop = openUp ? cursorY - popupH : cursorY;
                    originPoint = new Point(openLeft ? 1.0 : 0.0, openUp ? 1.0 : 0.0);
                } else {
                    targetLeft = (screenLeft + screenRight - popupW) / 2.0;
                    targetTop = (screenTop + screenBottom - popupH) / 2.0;
                    originPoint = new Point(0.5, 0.5);
                }
            }

            // Strict clamp in all 4 directions (Left, Right, Top, Bottom) - never exceeds monitor
            if (targetLeft + popupW > screenRight - 8) {
                targetLeft = screenRight - popupW - 8;
            }
            if (targetLeft < screenLeft + 8) {
                targetLeft = screenLeft + 8;
            }
            if (targetTop + popupH > screenBottom - 8) {
                targetTop = screenBottom - popupH - 8;
            }
            if (targetTop < screenTop + 8) {
                targetTop = screenTop + 8;
            }

            Left = targetLeft;
            Top = targetTop;

            _transformOrigin = originPoint;
            RootContainer.RenderTransformOrigin = originPoint;

            _anchorScreenPoint = new Point(
                (!_iconScreenBounds.IsEmpty && _iconScreenBounds.Width > 0)
                    ? (originPoint.X > 0.5 ? _iconScreenBounds.Right + 8 : _iconScreenBounds.Left - 8)
                    : ((originPoint.X > 0.5) ? (targetLeft + popupW) : targetLeft),
                (!_iconScreenBounds.IsEmpty && _iconScreenBounds.Height > 0)
                    ? (originPoint.Y > 0.5 ? _iconScreenBounds.Bottom + 8 : _iconScreenBounds.Top - 8)
                    : ((originPoint.Y > 0.5) ? (targetTop + popupH) : targetTop)
            );

            PopupScale.ScaleX = 0.12;
            PopupScale.ScaleY = 0.12;
            RootContainer.Opacity = 0.0;

            UpdatePopupBlurBehind(true, targetLeft, targetTop, popupW, popupH);
            ApplyThemeBackground();

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
                    UpdatePopupBlurBehind(forceImmediate: true);
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
            if (_isClosing) {
                return;
            }
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0) {
                return;
            }

            double newW = e.NewSize.Width;
            double newH = e.NewSize.Height;

            double deltaW = Math.Abs(newW - e.PreviousSize.Width);
            double deltaH = Math.Abs(newH - e.PreviousSize.Height);
            if (deltaW < 0.5 && deltaH < 0.5) {
                return;
            }

            if (!_isCustomPositioned) {
                var hwnd = new WindowInteropHelper(this).Handle;
                Rect monitorWorkArea = ShellHelper.GetMonitorWorkArea(hwnd != IntPtr.Zero ? hwnd : (IntPtr)0);
                if (monitorWorkArea.IsEmpty || monitorWorkArea.Width <= 0) {
                    monitorWorkArea = SystemParameters.WorkArea;
                }
                double screenLeft = monitorWorkArea.Left;
                double screenTop = monitorWorkArea.Top;
                double screenRight = monitorWorkArea.Right;
                double screenBottom = monitorWorkArea.Bottom;

                double newLeft;
                double newTop;

                if (SettingsService.Instance.Current.OpenInCenterOnly) {
                    newLeft = screenLeft + (screenRight - screenLeft - newW) / 2.0;
                    newTop = screenTop + (screenBottom - screenTop - newH) / 2.0;
                } else {
                    newLeft = (_transformOrigin.X > 0.5) ? (_anchorScreenPoint.X - newW) : _anchorScreenPoint.X;
                    newTop = (_transformOrigin.Y > 0.5) ? (_anchorScreenPoint.Y - newH) : _anchorScreenPoint.Y;

                    // If growing in current direction exceeds bounds, flip to opposite direction if it has more room
                    if (_transformOrigin.X <= 0.5 && newLeft + newW > screenRight - 8) {
                        double spaceLeft = _anchorScreenPoint.X - screenLeft;
                        double spaceRight = screenRight - _anchorScreenPoint.X;
                        if (spaceLeft > spaceRight || spaceLeft >= newW) {
                            newLeft = _anchorScreenPoint.X - newW;
                            _transformOrigin.X = 1.0;
                            RootContainer.RenderTransformOrigin = _transformOrigin;
                        }
                    } else if (_transformOrigin.X > 0.5 && newLeft < screenLeft + 8) {
                        double spaceLeft = _anchorScreenPoint.X - screenLeft;
                        double spaceRight = screenRight - _anchorScreenPoint.X;
                        if (spaceRight > spaceLeft || spaceRight >= newW) {
                            newLeft = _anchorScreenPoint.X;
                            _transformOrigin.X = 0.0;
                            RootContainer.RenderTransformOrigin = _transformOrigin;
                        }
                    }

                    if (_transformOrigin.Y <= 0.5 && newTop + newH > screenBottom - 8) {
                        double spaceUp = _anchorScreenPoint.Y - screenTop;
                        double spaceDown = screenBottom - _anchorScreenPoint.Y;
                        if (spaceUp > spaceDown || spaceUp >= newH) {
                            newTop = _anchorScreenPoint.Y - newH;
                            _transformOrigin.Y = 1.0;
                            RootContainer.RenderTransformOrigin = _transformOrigin;
                        }
                    } else if (_transformOrigin.Y > 0.5 && newTop < screenTop + 8) {
                        double spaceUp = _anchorScreenPoint.Y - screenTop;
                        double spaceDown = screenBottom - _anchorScreenPoint.Y;
                        if (spaceDown > spaceUp || spaceDown >= newH) {
                            newTop = _anchorScreenPoint.Y;
                            _transformOrigin.Y = 0.0;
                            RootContainer.RenderTransformOrigin = _transformOrigin;
                        }
                    }

                    // Strictly clamp in all directions so popup never exceeds monitor boundaries
                    if (newLeft + newW > screenRight - 8) {
                        newLeft = screenRight - newW - 8;
                    }
                    if (newLeft < screenLeft + 8) {
                        newLeft = screenLeft + 8;
                    }
                    if (newTop + newH > screenBottom - 8) {
                        newTop = screenBottom - newH - 8;
                    }
                    if (newTop < screenTop + 8) {
                        newTop = screenTop + 8;
                    }
                }

                if (Math.Abs(Left - newLeft) > 0.1) {
                    Left = newLeft;
                }
                if (Math.Abs(Top - newTop) > 0.1) {
                    Top = newTop;
                }
            }

            double cardW = Math.Max(10, newW);
            double cardH = Math.Max(10, newH);
            CardClipGeometry.Rect = new Rect(0, 0, cardW, cardH);

            if (PopupBlurImage.Visibility == Visibility.Visible) {
                double blurMargin = 25;
                PopupBlurImage.Width = cardW + blurMargin * 2;
                PopupBlurImage.Height = cardH + blurMargin * 2;
                UpdatePopupBlurBehind(false, Left, Top, cardW, cardH);
            }

            UpdateTitleMarquee(_folderData.Title);
        }

        private void InnerClippingBorder_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0) {
                CardClipGeometry.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
        }

        private bool _isEditPanelOpen = false;
        private double _normalPopupWidth = 0;

        private void CloseEditPanelImmediate() {
            _isEditPanelOpen = false;
            EditPanelContainer.BeginAnimation(HeightProperty, null);
            EditPanel.BeginAnimation(OpacityProperty, null);
            EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            this.BeginAnimation(WidthProperty, null);
            this.Width = double.NaN;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            if (_itemsUniformGrid != null) {
                _itemsUniformGrid.Columns = GetConfiguredColumns();
            }
            _normalPopupWidth = 0;
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
                this.BeginAnimation(WidthProperty, null);

                // Remember natural width before expanding
                if (_normalPopupWidth <= 0) {
                    _normalPopupWidth = ActualWidth > 0 ? ActualWidth : (GetConfiguredColumns() * ItemCellWidth + 24);
                }

                // Target width: EditPanel.Width (234) + 24px margins = 258px
                double targetWidth = EditPanel.Width + 24.0;
                double currentWidth = ActualWidth > 0 ? ActualWidth : _normalPopupWidth;

                // Temporarily allow explicit Width animation
                this.SizeToContent = SizeToContent.Height;
                if (_itemsUniformGrid != null) {
                    _itemsUniformGrid.Columns = 3;
                }

                // Measure natural height of EditPanel (fixed 3-column width)
                double availableWidth = 234;
                EditPanel.Measure(new Size(availableWidth, double.PositiveInfinity));
                double targetHeight = EditPanel.DesiredSize.Height;
                if (targetHeight <= 0) targetHeight = 150;

                double currentHeight = EditPanelContainer.ActualHeight;
                if (currentHeight < 0 || double.IsNaN(currentHeight)) currentHeight = 0;

                var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
                var heightAnim = new DoubleAnimation(currentHeight, targetHeight, duration) { EasingFunction = easeOut };
                var widthAnim = new DoubleAnimation(currentWidth, targetWidth, duration) { EasingFunction = easeOut };
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
                this.BeginAnimation(WidthProperty, widthAnim);
                EditPanel.BeginAnimation(OpacityProperty, opacityAnim);
                EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
            } else {
                double startHeight = EditPanelContainer.ActualHeight;
                double startWidth = ActualWidth;
                double returnWidth = _normalPopupWidth > 0 ? _normalPopupWidth : (GetConfiguredColumns() * ItemCellWidth + 24);

                if (startHeight <= 0) {
                    CloseEditPanelImmediate();
                    return;
                }

                // Stop any running animations
                EditPanelContainer.BeginAnimation(HeightProperty, null);
                EditPanel.BeginAnimation(OpacityProperty, null);
                EditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                this.BeginAnimation(WidthProperty, null);

                var easeIn = new CubicEase { EasingMode = EasingMode.EaseInOut };
                var heightAnim = new DoubleAnimation(startHeight, 0.0, duration) { EasingFunction = easeIn };
                var widthAnim = new DoubleAnimation(startWidth, returnWidth, duration) { EasingFunction = easeIn };
                var opacityAnim = new DoubleAnimation(EditPanel.Opacity, 0.0, duration) { EasingFunction = easeIn };
                var translateAnim = new DoubleAnimation(EditPanelTranslate.Y, -10.0, duration) { EasingFunction = easeIn };

                heightAnim.Completed += (s, e) => {
                    if (!_isEditPanelOpen) {
                        EditPanelContainer.BeginAnimation(HeightProperty, null);
                        EditPanelContainer.Height = 0;
                        EditPanelContainer.Visibility = Visibility.Collapsed;
                    }
                };

                widthAnim.Completed += (s, e) => {
                    if (!_isEditPanelOpen) {
                        this.BeginAnimation(WidthProperty, null);
                        this.Width = double.NaN;
                        this.SizeToContent = SizeToContent.WidthAndHeight;
                        if (_itemsUniformGrid != null) {
                            _itemsUniformGrid.Columns = GetConfiguredColumns();
                        }
                        _normalPopupWidth = 0;
                    }
                };

                EditPanelContainer.BeginAnimation(HeightProperty, heightAnim);
                this.BeginAnimation(WidthProperty, widthAnim);
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
            if (!string.IsNullOrEmpty(_rootFolderPath)) {
                FolderLockService.Instance.RelockFolderSession(_rootFolderPath);
            }
            FlushPendingSettingsSave();
            EnsurePanakJsonSync();
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

        private long _lastBlurCaptureTicks = 0;
        private bool _isCapturingBlur = false;

        public void UpdatePopupBlurBehind(bool forceImmediate = false, double? overrideLeft = null, double? overrideTop = null, double? overrideW = null, double? overrideH = null) {
            try {
                if (!SettingsService.Instance.Current.FolderBlurEnabled || SettingsService.Instance.Current.FolderBlurRadius <= 0) {
                    PopupBlurImage.Source = null;
                    PopupBlurImage.Visibility = Visibility.Collapsed;
                    return;
                }

                long now = DateTime.UtcNow.Ticks;
                if (!forceImmediate && _lastBlurCaptureTicks > 0 && (now - _lastBlurCaptureTicks) < TimeSpan.FromMilliseconds(25).Ticks) {
                    return;
                }
                if (_isCapturingBlur) return;
                _isCapturingBlur = true;
                _lastBlurCaptureTicks = now;

                double dpi = 1.0;
                try {
                    dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                } catch { }
                if (dpi <= 0) dpi = 1.0;

                double cardW = Math.Max(10, overrideW ?? (ActualWidth > 0 ? ActualWidth : Width));
                double cardH = Math.Max(10, overrideH ?? (ActualHeight > 0 ? ActualHeight : Height));
                double blurMargin = 25;
                double imgW = cardW + blurMargin * 2;
                double imgH = cardH + blurMargin * 2;

                CardClipGeometry.Rect = new Rect(0, 0, cardW, cardH);
                Canvas.SetLeft(PopupBlurImage, -blurMargin);
                Canvas.SetTop(PopupBlurImage, -blurMargin);
                PopupBlurImage.Width = imgW;
                PopupBlurImage.Height = imgH;

                double cardLeft = overrideLeft ?? Left;
                double cardTop = overrideTop ?? Top;
                double capLeft = cardLeft - blurMargin;
                double capTop = cardTop - blurMargin;

                bool isHwAccelerated = (RenderCapability.Tier >> 16) >= 2;
                var screenBmp = ShellHelper.CaptureScreenArea(
                    (int)Math.Round(capLeft),
                    (int)Math.Round(capTop),
                    (int)Math.Round(imgW),
                    (int)Math.Round(imgH),
                    dpi,
                    downsampleForBlur: true);

                if (screenBmp != null) {
                    PopupBlurImage.Source = screenBmp;
                    PopupBlurImage.Visibility = Visibility.Visible;
                    double radius = Math.Clamp(SettingsService.Instance.Current.FolderBlurRadius, 1.0, 60.0);
                    if (!isHwAccelerated) {
                        radius = Math.Min(radius, 14.0);
                    }
                    PopupBlurEffect.Radius = radius;
                } else {
                    PopupBlurImage.Source = null;
                    PopupBlurImage.Visibility = Visibility.Collapsed;
                }
                ApplyThemeBackground();
            } catch {
                PopupBlurImage.Source = null;
                PopupBlurImage.Visibility = Visibility.Collapsed;
                ApplyThemeBackground();
            } finally {
                _isCapturingBlur = false;
            }
        }

        // Header Actions
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed) {
                var dep = e.OriginalSource as DependencyObject;
                if (FindVisualParent<Button>(dep) == null &&
                    FindVisualParent<TextBox>(dep) == null &&
                    !IsDescendantOf(dep, TitleContainer)) {
                    if (SettingsService.Instance.Current.LockPopupPosition) {
                        return;
                    }
                    _isCustomPositioned = true;
                    try {
                        DragMove();
                    } catch { }
                    UpdatePopupBlurBehind(true);
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject? target) {
            if (child == null || target == null) return false;
            while (child != null) {
                if (ReferenceEquals(child, target)) return true;
                if (child is Visual || child is System.Windows.Media.Media3D.Visual3D) {
                    child = VisualTreeHelper.GetParent(child);
                } else {
                    child = LogicalTreeHelper.GetParent(child);
                }
            }
            return false;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject {
            while (child != null) {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void PinButton_Click(object sender, RoutedEventArgs e) {
            TogglePin();
        }

        public void TogglePin() {
            _isPinned = !_isPinned;
            UpdatePinVisual();
            if (_isPinned) {
                PanakManager.Instance.TemporarilyHideBackdropForDrag();
            } else {
                PanakManager.Instance.RestoreBackdropAfterDrag();
            }
            PanakManager.Instance.EnsurePopupAboveBackdrop();
        }

        private void UpdatePinVisual() {
            // Popup window must always stay Topmost so it never drops behind background or desktop windows
            Topmost = true;
            var loc = LocalizationService.Instance;
            PinButton.ToolTip = _isPinned ? loc["Unpin"] : loc["Pin"];
            if (_isPinned) {
                PinButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 255, 255, 255));
                PinIconPath.Fill = (Brush)FindResource("M3PrimaryBrush");
            } else {
                PinButton.ClearValue(Button.BackgroundProperty);
                PinIconPath.Fill = (Brush)FindResource("PopupHeaderBtnFg");
            }
        }

        private void UpdateTitleMarquee(string title) {
            FolderTitleText.Text = title;
            FolderTitleDuplicateText.Text = title;

            FolderTitleText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = FolderTitleText.DesiredSize.Width;

            double maxTitleWidth = 110;
            if (ActualWidth > 0) {
                maxTitleWidth = Math.Max(100, Math.Min(180, ActualWidth - 140));
            }
            TitleContainer.MaxWidth = maxTitleWidth;

            double displayWidth = Math.Min(textWidth, maxTitleWidth);
            if (displayWidth < 10) displayWidth = 10;
            TitleContainer.Width = displayWidth;
            TitleMarqueeCanvas.Width = displayWidth;

            if (textWidth > maxTitleWidth + 2) {
                FolderTitleDuplicateText.Visibility = Visibility.Visible;
                double gap = 28.0;
                double totalDistance = textWidth + gap;
                double durationSeconds = Math.Max(3.0, totalDistance / 35.0);

                var anim = new DoubleAnimationUsingKeyFrames();
                anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.5))));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(-totalDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.5 + durationSeconds))));
                anim.RepeatBehavior = RepeatBehavior.Forever;

                TitleMarqueeTransform.BeginAnimation(TranslateTransform.XProperty, anim);
            } else {
                FolderTitleDuplicateText.Visibility = Visibility.Collapsed;
                TitleMarqueeTransform.BeginAnimation(TranslateTransform.XProperty, null);
                TitleMarqueeTransform.X = 0;
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e) {
            ToggleEditPanel();
        }

        private void ExplorerButton_Click(object sender, RoutedEventArgs e) {
            if (FolderLockService.Instance.IsFolderLocked(_rootFolderPath)) {
                return;
            }
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
            EnsurePanakJsonSync();
            PanakManager.Instance.WatchFolder(_rootFolderPath);
            PanakManager.Instance.ScheduleFolderIconUpdate(_rootFolderPath, immediate: true);
        }

        private void AlwaysPanakCheckBox_Unchecked(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            if (!string.IsNullOrEmpty(_rootFolderPath) && (FolderLockService.Instance.IsFolderLocked(_rootFolderPath) || FolderLockService.Instance.GetFolderLockInfo(_rootFolderPath)?.IsLocked == true)) {
                AlwaysPanakCheckBox.IsChecked = true;
                return;
            }
            _folderData.AutoPopup = false;
            EnsurePanakJsonSync();
            PanakManager.Instance.RevertToRegularFolder(_rootFolderPath);
        }

        // Inline Title Rename via Double-click
        public void FolderTitleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (InlineNameEditBox.Visibility == Visibility.Visible) {
                return;
            }
            if (e.ClickCount >= 2) {
                StartInlineRename();
                e.Handled = true;
            } else if (e.ClickCount == 1) {
                // Prevent single-click bubbling up to Header to avoid accidental DragMove
                e.Handled = true;
            }
        }

        public void StartInlineRename() {
            TitleMarqueeTransform.BeginAnimation(TranslateTransform.XProperty, null);
            TitleMarqueeTransform.X = 0;
            FolderTitleDuplicateText.Visibility = Visibility.Collapsed;
            TitleMarqueeCanvas.Visibility = Visibility.Collapsed;
            TitleContainer.Width = double.NaN;
            TitleContainer.MaxWidth = ActualWidth > 0 ? Math.Max(180, ActualWidth - 120) : double.PositiveInfinity;
            InlineNameEditBox.Text = _folderData.Title;
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

                UpdateTitleMarquee(newName);
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

            UpdateTitleMarquee(newName);
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
            TitleMarqueeCanvas.Visibility = Visibility.Visible;
            FolderTitleText.Visibility = Visibility.Visible;

            if (!string.IsNullOrEmpty(newName) && !newName.Equals(_folderData.Title)) {
                RenameFolder(newName);
            } else {
                UpdateTitleMarquee(_folderData.Title);
            }
        }

        private void InlineNameEditBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SaveInlineRename();
                e.Handled = true;
            } else if (e.Key == Key.Escape) {
                InlineNameEditBox.Visibility = Visibility.Collapsed;
                TitleMarqueeCanvas.Visibility = Visibility.Visible;
                FolderTitleText.Visibility = Visibility.Visible;
                UpdateTitleMarquee(_folderData.Title);
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

        private System.Windows.Threading.DispatcherTimer? _debouncedSettingsSaveTimer;

        private void CancelPendingDebouncedSave() {
            if (_debouncedSettingsSaveTimer != null) {
                try {
                    _debouncedSettingsSaveTimer.Stop();
                } catch { }
                _debouncedSettingsSaveTimer = null;
            }
        }

        private void ScheduleDebouncedFolderSettingsSave(bool immediate) {
            string folderPath = _rootFolderPath;
            if (string.IsNullOrEmpty(folderPath)) return;

            CancelPendingDebouncedSave();

            if (AlwaysPanakCheckBox.IsChecked != true) {
                EnsurePanakJsonSync();
                return;
            }

            if (immediate) {
                try {
                    _folderData.AutoPopup = true;
                    _folderData.Save(folderPath);
                    PanakManager.Instance.ScheduleFolderIconUpdate(folderPath, immediate: false, shape: _folderData.IconShape, color: _folderData.Color);
                } catch { }
                return;
            }

            _debouncedSettingsSaveTimer = new System.Windows.Threading.DispatcherTimer(
                TimeSpan.FromMilliseconds(350),
                System.Windows.Threading.DispatcherPriority.Background,
                (s, e) => {
                    CancelPendingDebouncedSave();
                    try {
                        if (AlwaysPanakCheckBox.IsChecked == true) {
                            _folderData.AutoPopup = true;
                            _folderData.Save(folderPath);
                            PanakManager.Instance.ScheduleFolderIconUpdate(folderPath, immediate: false, shape: _folderData.IconShape, color: _folderData.Color);
                        } else {
                            EnsurePanakJsonSync();
                        }
                    } catch { }
                },
                Dispatcher
            );
            _debouncedSettingsSaveTimer.Start();
        }

        private void FlushPendingSettingsSave() {
            string folderPath = _rootFolderPath;
            if (string.IsNullOrEmpty(folderPath)) return;

            if (_debouncedSettingsSaveTimer != null) {
                CancelPendingDebouncedSave();
                try {
                    if (AlwaysPanakCheckBox.IsChecked == true) {
                        _folderData.AutoPopup = true;
                        _folderData.Save(folderPath);
                        PanakManager.Instance.ScheduleFolderIconUpdate(folderPath, immediate: false, shape: _folderData.IconShape, color: _folderData.Color);
                    } else {
                        EnsurePanakJsonSync();
                    }
                } catch { }
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

        public void ApplyOpacity(double opacity) {
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

                var textColor = isDark ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(0, 0, 0);
                var subtextColor = isDark ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(0, 0, 0);

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

                    FolderTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    EmptyTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    EmptySubtext.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    EditNameLabel.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    ColorThemeLabel.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    AlwaysPanakCheckBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    FolderLockHeaderLabel.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);

                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupHeaderBtnFg"] = new SolidColorBrush(System.Windows.Media.Colors.White);
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    Resources["PopupItemBadgeFg"] = new SolidColorBrush(System.Windows.Media.Colors.White);
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 255, 255, 255));
                } else {
                    // Light Theme
                    CardBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0));
                    CardBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255));

                    FolderTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    EmptyTitleText.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    EmptySubtext.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    EditNameLabel.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    ColorThemeLabel.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    AlwaysPanakCheckBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    FolderLockHeaderLabel.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);

                    Resources["PopupHeaderBtnBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 0, 0, 0));
                    Resources["PopupHeaderBtnFg"] = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    Resources["PopupItemBadgeBg"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                    Resources["PopupItemBadgeFg"] = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    Resources["PopupItemHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                }

                FolderTitleDuplicateText.Foreground = FolderTitleText.Foreground;
                UpdatePinVisual();
                if (PopupLockOverlay != null) {
                    PopupLockOverlay.ApplyThemeContrast(isDark);
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
                        string resolvedPath = item.FullPath;
                        if (string.Equals(item.Extension, ".lnk", StringComparison.OrdinalIgnoreCase)) {
                            string? target = ShellHelper.ResolveShortcut(item.FullPath);
                            if (!string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target))) {
                                resolvedPath = target;
                            }
                        }

                        string[] files = new[] { resolvedPath };
                        DataObject data = new DataObject(DataFormats.FileDrop, files);
                        PanakManager.Instance.TemporarilyHideBackdropForDrag();
                        try {
                            DragDropEffects effect = DragDrop.DoDragDrop(elem, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
                        } finally {
                            if (!_isPinned) {
                                PanakManager.Instance.RestoreBackdropAfterDrag();
                            }
                        }

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
            if (FolderLockService.Instance.IsFolderLocked(_rootFolderPath)) {
                return;
            }
            if (sender is MenuItem mi && mi.DataContext is PanakItem item) {
                ShellContextMenu.ShowInExplorer(item.FullPath);
                CloseAnimated();
            }
        }

        private void MenuItemRemove_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi && mi.DataContext is PanakItem item) {
                try {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = Path.GetFileName(item.FullPath);
                    string target = Path.Combine(desktop, fileName);
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

        private void MenuItemUnpack_Click(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(_rootFolderPath)) {
                PanakManager.Instance.UnpackPanakFolder(_rootFolderPath);
                CloseAnimated();
            }
        }

        private readonly List<int> _inlineSetupPatternDots = new();
        private bool _isInlineSetupDrawing = false;
        private readonly Point[] _inlineSetupPatternDotPositions = new Point[9];
        private readonly Ellipse[] _inlineSetupPatternDotElements = new Ellipse[9];
        private readonly Ellipse[] _inlineSetupPatternDotRings = new Ellipse[9];
        private Polyline? _inlineSetupPolyline;
        private Line? _inlineSetupActiveLine;
        private double _widthBeforeLockSetup = 0;

        private void FolderLockBtn_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(_rootFolderPath)) return;
            var lockInfo = FolderLockService.Instance.GetFolderLockInfo(_rootFolderPath);
            bool hasConfiguredLock = lockInfo != null && lockInfo.IsLocked;

            ToggleEditPanel(forceOpen: false);

            if (hasConfiguredLock) {
                // In-place credential prompt to remove lock
                ItemsScrollViewer.Visibility = Visibility.Collapsed;
                SliderPagesViewport.Visibility = Visibility.Collapsed;
                FooterPagerGrid.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                LockSetupPanel.Visibility = Visibility.Collapsed;

                PopupLockOverlay.SetupForRemoveLock(_rootFolderPath);
                PopupLockOverlay.ApplyFolderTheme(_folderData.Color, _isCurrentThemeDark);
                PopupLockOverlay.Visibility = Visibility.Visible;
            } else {
                // In-place lock setup view
                ShowLockSetupView();
            }
        }

        private void UpdateLockState() {
            if (string.IsNullOrEmpty(_rootFolderPath)) return;
            bool isLocked = FolderLockService.Instance.IsFolderLocked(_rootFolderPath);
            var lockInfo = FolderLockService.Instance.GetFolderLockInfo(_rootFolderPath);
            bool hasConfiguredLock = lockInfo != null && lockInfo.IsLocked;

            if (hasConfiguredLock) {
                FolderLockHeaderLabel.Text = "Պանակը կողպված է";
                FolderLockBtnIcon.Text = "🔓 ";
                FolderLockBtnText.Text = "Հեռացնել փական";
            } else {
                FolderLockHeaderLabel.Text = "Կողպել պանակը";
                FolderLockBtnIcon.Text = "🔒 ";
                FolderLockBtnText.Text = "Կողպել պանակը";
            }

            if (isLocked) {
                PopupLockOverlay.SetupForFolder(_rootFolderPath);
                PopupLockOverlay.ApplyFolderTheme(_folderData.Color, _isCurrentThemeDark);
                PopupLockOverlay.Visibility = Visibility.Visible;
                LockSetupPanel.Visibility = Visibility.Collapsed;
                ItemsScrollViewer.Visibility = Visibility.Collapsed;
                SliderPagesViewport.Visibility = Visibility.Collapsed;
                FooterPagerGrid.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                ExplorerButton.Visibility = Visibility.Collapsed;
            } else {
                PopupLockOverlay.Visibility = Visibility.Collapsed;
                LockSetupPanel.Visibility = Visibility.Collapsed;
                ExplorerButton.Visibility = Visibility.Visible;
                ApplySettings();
                RefreshItemsList();
                UpdateItemCountText();
            }
            UpdateDisplayAffinity();
        }

        private void ShowLockSetupView() {
            _widthBeforeLockSetup = ActualWidth > 0 ? ActualWidth : (GetConfiguredColumns() * ItemCellWidth + 24);
            if (ActualWidth < 300) {
                this.Width = 310;
            }

            ItemsScrollViewer.Visibility = Visibility.Collapsed;
            SliderPagesViewport.Visibility = Visibility.Collapsed;
            FooterPagerGrid.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            PopupLockOverlay.Visibility = Visibility.Collapsed;

            SetupPasswordInput.Password = string.Empty;
            SetupPasswordConfirm.Password = string.Empty;
            SetupPinInput.Password = string.Empty;
            SetupPinConfirm.Password = string.Empty;
            _inlineSetupPatternDots.Clear();
            SetupErrorBadge.Visibility = Visibility.Collapsed;

            SetupMethodPasswordRadio.IsChecked = true;
            SetupPasswordContainer.Visibility = Visibility.Visible;
            SetupPinContainer.Visibility = Visibility.Collapsed;
            SetupPatternContainer.Visibility = Visibility.Collapsed;

            LockSetupPanel.Visibility = Visibility.Visible;
            SetupPasswordInput.Focus();
        }

        private void HideLockSetupView() {
            LockSetupPanel.Visibility = Visibility.Collapsed;
            if (_widthBeforeLockSetup > 0) {
                this.Width = _widthBeforeLockSetup;
            } else {
                this.Width = double.NaN;
            }
            UpdateLockState();
        }

        private void SetupCancelBtn_Click(object sender, RoutedEventArgs e) {
            HideLockSetupView();
        }

        private void SetupMethodRadio_Checked(object sender, RoutedEventArgs e) {
            if (SetupPasswordContainer == null || SetupPinContainer == null || SetupPatternContainer == null) return;

            double oldHeight = ActualHeight;

            SetupPasswordContainer.Visibility = Visibility.Collapsed;
            SetupPinContainer.Visibility = Visibility.Collapsed;
            SetupPatternContainer.Visibility = Visibility.Collapsed;
            SetupErrorBadge.Visibility = Visibility.Collapsed;

            if (SetupMethodPasswordRadio.IsChecked == true) {
                SetupPasswordContainer.Visibility = Visibility.Visible;
                SetupPasswordInput.Focus();
            } else if (SetupMethodPinRadio.IsChecked == true) {
                SetupPinContainer.Visibility = Visibility.Visible;
                SetupPinInput.Focus();
            } else if (SetupMethodPatternRadio.IsChecked == true) {
                SetupPatternContainer.Visibility = Visibility.Visible;
                InitInlineSetupPatternCanvas();
                ResetInlineSetupPatternVisuals();
            }

            if (IsLoaded && LockSetupPanel.Visibility == Visibility.Visible && oldHeight > 0) {
                UpdateLayout();
                RootContainer.Measure(new Size(ActualWidth > 0 ? ActualWidth : 310, double.PositiveInfinity));
                double targetHeight = RootContainer.DesiredSize.Height;
                if (targetHeight > 0 && Math.Abs(targetHeight - oldHeight) > 3) {
                    this.SizeToContent = SizeToContent.Manual;
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    var anim = new DoubleAnimation(oldHeight, targetHeight, new Duration(TimeSpan.FromMilliseconds(220))) {
                        EasingFunction = ease
                    };
                    anim.Completed += (s, a) => {
                        this.BeginAnimation(HeightProperty, null);
                        this.Height = double.NaN;
                        this.SizeToContent = SizeToContent.Height;
                    };
                    this.BeginAnimation(HeightProperty, anim);
                }
            }
        }

        private void SetupSaveBtn_Click(object sender, RoutedEventArgs e) {
            SetupErrorBadge.Visibility = Visibility.Collapsed;
            string lockType = "Password";
            string credential = string.Empty;

            if (SetupMethodPasswordRadio.IsChecked == true) {
                lockType = "Password";
                string p1 = SetupPasswordInput.Password;
                string p2 = SetupPasswordConfirm.Password;
                if (string.IsNullOrEmpty(p1)) {
                    ShowSetupError("Խնդրում ենք մուտքագրել գաղտնաբառ:");
                    return;
                }
                if (p1 != p2) {
                    ShowSetupError("Գաղտնաբառերը չեն համընկնում:");
                    return;
                }
                credential = p1;
            } else if (SetupMethodPinRadio.IsChecked == true) {
                lockType = "PIN";
                string pin1 = SetupPinInput.Password;
                string pin2 = SetupPinConfirm.Password;
                if (string.IsNullOrEmpty(pin1) || pin1.Length < 4 || pin1.Length > 8 || !pin1.All(char.IsDigit)) {
                    ShowSetupError("PIN կոդը պետք է բաղկացած լինի 4-ից 8 թվանշաններից:");
                    return;
                }
                if (pin1 != pin2) {
                    ShowSetupError("PIN կոդերը չեն համընկնում:");
                    return;
                }
                credential = pin1;
            } else if (SetupMethodPatternRadio.IsChecked == true) {
                lockType = "Pattern";
                if (_inlineSetupPatternDots.Count < 4) {
                    ShowSetupError("Նախշը պետք է պարունակի առնվազն 4 կետ:");
                    return;
                }
                credential = string.Join("-", _inlineSetupPatternDots);
            }

            FolderLockService.Instance.SetFolderLock(_rootFolderPath, lockType, credential, null);
            _folderData.AutoPopup = true;
            AlwaysPanakCheckBox.IsChecked = true;
            EnsurePanakJsonSync();
            PanakManager.Instance.WatchFolder(_rootFolderPath);
            PanakManager.Instance.ScheduleFolderIconUpdate(_rootFolderPath, immediate: true);
            HideLockSetupView();
        }

        private void ShowSetupError(string msg) {
            SetupErrorText.Text = msg;
            SetupErrorBadge.Visibility = Visibility.Visible;
        }

        private void InitInlineSetupPatternCanvas() {
            if (InlineSetupPatternCanvas == null) return;
            InlineSetupPatternCanvas.Children.Clear();

            Brush primaryBrush = TryFindResource("M3PrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(234, 88, 12));
            Brush dotFillBrush = TryFindResource("PopupSubtextBrush") as Brush ?? new SolidColorBrush(Color.FromArgb(120, 150, 150, 150));

            _inlineSetupPolyline = new Polyline {
                Stroke = primaryBrush,
                StrokeThickness = 3.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            InlineSetupPatternCanvas.Children.Add(_inlineSetupPolyline);

            _inlineSetupActiveLine = new Line {
                Stroke = primaryBrush,
                Opacity = 0.6,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };
            InlineSetupPatternCanvas.Children.Add(_inlineSetupActiveLine);

            double w = 170;
            double h = 170;
            double stepX = w / 3.0;
            double stepY = h / 3.0;

            for (int i = 0; i < 9; i++) {
                int row = i / 3;
                int col = i % 3;
                double cx = col * stepX + stepX / 2.0;
                double cy = row * stepY + stepY / 2.0;
                _inlineSetupPatternDotPositions[i] = new Point(cx, cy);

                var ring = new Ellipse {
                    Width = 32,
                    Height = 32,
                    Stroke = primaryBrush,
                    StrokeThickness = 2,
                    Visibility = Visibility.Collapsed
                };
                Canvas.SetLeft(ring, cx - 16);
                Canvas.SetTop(ring, cy - 16);
                InlineSetupPatternCanvas.Children.Add(ring);
                _inlineSetupPatternDotRings[i] = ring;

                var dot = new Ellipse {
                    Width = 14,
                    Height = 14,
                    Fill = dotFillBrush
                };
                Canvas.SetLeft(dot, cx - 7);
                Canvas.SetTop(dot, cy - 7);
                InlineSetupPatternCanvas.Children.Add(dot);
                _inlineSetupPatternDotElements[i] = dot;
            }
        }

        private void InlineSetupPatternCanvas_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            _isInlineSetupDrawing = true;
            _inlineSetupPatternDots.Clear();
            ResetInlineSetupPatternVisuals();
            SetupErrorBadge.Visibility = Visibility.Collapsed;

            Point p = e.GetPosition(InlineSetupPatternCanvas);
            CheckInlineSetupHitDot(p);
            InlineSetupPatternCanvas.CaptureMouse();
        }

        private void InlineSetupPatternCanvas_MouseMove(object sender, MouseEventArgs e) {
            if (!_isInlineSetupDrawing) return;
            Point p = e.GetPosition(InlineSetupPatternCanvas);
            CheckInlineSetupHitDot(p);

            if (_inlineSetupPatternDots.Count > 0 && _inlineSetupActiveLine != null) {
                var lastDot = _inlineSetupPatternDotPositions[_inlineSetupPatternDots.Last() - 1];
                _inlineSetupActiveLine.X1 = lastDot.X;
                _inlineSetupActiveLine.Y1 = lastDot.Y;
                _inlineSetupActiveLine.X2 = p.X;
                _inlineSetupActiveLine.Y2 = p.Y;
                _inlineSetupActiveLine.Visibility = Visibility.Visible;
            }
        }

        private void InlineSetupPatternCanvas_MouseUp(object sender, MouseButtonEventArgs e) {
            FinishInlineSetupDrawing();
        }

        private void InlineSetupPatternCanvas_MouseLeave(object sender, MouseEventArgs e) {
            if (_isInlineSetupDrawing && !InlineSetupPatternCanvas.IsMouseCaptured) {
                FinishInlineSetupDrawing();
            }
        }

        private void FinishInlineSetupDrawing() {
            if (!_isInlineSetupDrawing) return;
            _isInlineSetupDrawing = false;
            if (InlineSetupPatternCanvas.IsMouseCaptured) {
                InlineSetupPatternCanvas.ReleaseMouseCapture();
            }
            if (_inlineSetupActiveLine != null) {
                _inlineSetupActiveLine.Visibility = Visibility.Collapsed;
            }

            UpdateInlinePatternDisplay();
            if (_inlineSetupPatternDots.Count > 0 && _inlineSetupPatternDots.Count < 4) {
                ShowSetupError("Նախշը պետք է ունենա առնվազն 4 կետ");
            }
        }

        private static int GetIntermediateDot(int a, int b) {
            if (a > b) (a, b) = (b, a);
            return (a, b) switch {
                (1, 3) => 2,
                (4, 6) => 5,
                (7, 9) => 8,
                (1, 7) => 4,
                (2, 8) => 5,
                (3, 9) => 6,
                (1, 9) => 5,
                (3, 7) => 5,
                _ => 0
            };
        }

        private void SelectInlineSetupDot(int val) {
            if (_inlineSetupPatternDots.Contains(val) || val < 1 || val > 9) return;
            int idx = val - 1;
            _inlineSetupPatternDots.Add(val);
            HighlightInlineSetupDot(idx);
            _inlineSetupPolyline?.Points.Add(_inlineSetupPatternDotPositions[idx]);
            UpdateInlinePatternDisplay();
        }

        private void CheckInlineSetupHitDot(Point p) {
            double radiusSq = 20 * 20;

            if (_inlineSetupPatternDots.Count == 0) {
                for (int i = 0; i < 9; i++) {
                    int val = i + 1;
                    var dotPos = _inlineSetupPatternDotPositions[i];
                    double dx = p.X - dotPos.X;
                    double dy = p.Y - dotPos.Y;
                    if (dx * dx + dy * dy <= radiusSq) {
                        SelectInlineSetupDot(val);
                        break;
                    }
                }
                return;
            }

            Point start = _inlineSetupPatternDotPositions[_inlineSetupPatternDots.Last() - 1];
            Point end = p;

            double segDx = end.X - start.X;
            double segDy = end.Y - start.Y;
            double segLenSq = segDx * segDx + segDy * segDy;

            var crossedDots = new List<(int val, double t)>();

            for (int i = 0; i < 9; i++) {
                int val = i + 1;
                if (_inlineSetupPatternDots.Contains(val)) continue;

                var dotPos = _inlineSetupPatternDotPositions[i];
                double distSq;
                double t;

                if (segLenSq < 0.001) {
                    double ddx = p.X - dotPos.X;
                    double ddy = p.Y - dotPos.Y;
                    distSq = ddx * ddx + ddy * ddy;
                    t = 0;
                } else {
                    t = ((dotPos.X - start.X) * segDx + (dotPos.Y - start.Y) * segDy) / segLenSq;
                    double tClamped = Math.Max(0, Math.Min(1, t));
                    double projX = start.X + tClamped * segDx;
                    double projY = start.Y + tClamped * segDy;
                    double ddx = dotPos.X - projX;
                    double ddy = dotPos.Y - projY;
                    distSq = ddx * ddx + ddy * ddy;
                }

                if (distSq <= radiusSq) {
                    crossedDots.Add((val, t));
                }
            }

            crossedDots.Sort((a, b) => a.t.CompareTo(b.t));

            foreach (var (val, _) in crossedDots) {
                if (_inlineSetupPatternDots.Contains(val)) continue;
                if (_inlineSetupPatternDots.Count > 0) {
                    int mid = GetIntermediateDot(_inlineSetupPatternDots.Last(), val);
                    if (mid > 0 && !_inlineSetupPatternDots.Contains(mid)) {
                        SelectInlineSetupDot(mid);
                    }
                }
                SelectInlineSetupDot(val);
            }
        }

        private void HighlightInlineSetupDot(int i) {
            if (i >= 0 && i < 9) {
                Brush primaryBrush = TryFindResource("M3PrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(234, 88, 12));
                if (_inlineSetupPatternDotElements[i] != null) {
                    _inlineSetupPatternDotElements[i].Fill = primaryBrush;
                }
                if (_inlineSetupPatternDotRings[i] != null) {
                    _inlineSetupPatternDotRings[i].Visibility = Visibility.Visible;
                }
            }
        }

        private void ResetInlineSetupPatternVisuals() {
            if (_inlineSetupPolyline != null) {
                _inlineSetupPolyline.Points.Clear();
            }
            if (_inlineSetupActiveLine != null) {
                _inlineSetupActiveLine.Visibility = Visibility.Collapsed;
            }
            Brush dotFillBrush = TryFindResource("PopupSubtextBrush") as Brush ?? new SolidColorBrush(Color.FromArgb(120, 150, 150, 150));
            for (int i = 0; i < 9; i++) {
                if (_inlineSetupPatternDotElements[i] != null) {
                    _inlineSetupPatternDotElements[i].Fill = dotFillBrush;
                }
                if (_inlineSetupPatternDotRings[i] != null) {
                    _inlineSetupPatternDotRings[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdateInlinePatternDisplay() {
            if (_inlineSetupPatternDots.Count == 0) {
                InlineSetupPatternText.Text = "Ընտրված կետեր՝ (չկա)";
            } else {
                InlineSetupPatternText.Text = "Ընտրված կետեր՝ " + string.Join(" ➔ ", _inlineSetupPatternDots);
            }
        }

        private void InlineSetupPatternClear_Click(object sender, RoutedEventArgs e) {
            _inlineSetupPatternDots.Clear();
            ResetInlineSetupPatternVisuals();
            UpdateInlinePatternDisplay();
            SetupErrorBadge.Visibility = Visibility.Collapsed;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e) {
            if (sender is ContextMenu cm) {
                var loc = LocalizationService.Instance;
                bool isLocked = FolderLockService.Instance.IsFolderLocked(_rootFolderPath);
                foreach (var item in cm.Items) {
                    if (item is MenuItem mi) {
                        string header = mi.Header?.ToString() ?? string.Empty;
                        if (header == "Բացել" || header.StartsWith("Open") || header == loc["Open"]) {
                            mi.Header = loc["Open"];
                        } else if (header.Contains("Explorer")) {
                            mi.Header = loc["OpenInExplorer"];
                            mi.Visibility = isLocked ? Visibility.Collapsed : Visibility.Visible;
                        } else if (header.Contains("պանակից") || header.Contains("folder") || header == loc["RemoveFromFolder"]) {
                            mi.Header = loc["RemoveFromFolder"];
                        } else if (header.Contains("Ապահավաքել") || header.Contains("Unpack") || header == loc["UnpackPanakFolder"]) {
                            mi.Header = loc["UnpackPanakFolder"];
                        } else if (header == "Ջնջել" || header == "Delete" || header == loc["Delete"]) {
                            mi.Header = loc["Delete"];
                        } else if (header == "Հատկություններ" || header == "Properties" || header == loc["Properties"]) {
                            mi.Header = loc["Properties"];
                        }
                    }
                }
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                if (InlineNameEditBox.Visibility == Visibility.Visible) {
                    InlineNameEditBox.Visibility = Visibility.Collapsed;
                    TitleMarqueeCanvas.Visibility = Visibility.Visible;
                    FolderTitleText.Visibility = Visibility.Visible;
                    UpdateTitleMarquee(_folderData.Title);
                    e.Handled = true;
                    return;
                }
                if (LockSetupPanel.Visibility == Visibility.Visible) {
                    HideLockSetupView();
                    e.Handled = true;
                    return;
                }
                if (PopupLockOverlay.Visibility == Visibility.Visible && PopupLockOverlay.IsRemoveLockMode) {
                    PopupLockOverlay.CancelRemoveLock();
                    UpdateLockState();
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
