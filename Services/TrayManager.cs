using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Panak.Views;
using Panak.Models;

namespace Panak.Services {
    public class TrayManager : IDisposable {
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 101;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;
        private const int NIIF_INFO = 0x00000001;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        private HwndSource? _hwndSource;
        private IntPtr _hIcon = IntPtr.Zero;
        private bool _isAdded = false;
        private int _wmTaskbarCreated = 0;

        public event Action? SettingsRequested;
        public event Action? NewFolderRequested;
        public event Action? ExitRequested;

        public void Initialize() {
            // Create hidden message-only window
            HwndSourceParameters parameters = new HwndSourceParameters("PanakTrayMsgWindow") {
                WindowStyle = 0,
                ExtendedWindowStyle = 0
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(HwndMessageHook);

            _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

            AddOrUpdateTrayIcon();
        }

        private IntPtr LoadPanakTrayIcon() {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = Path.Combine(baseDir, "Panak.ico");
                if (File.Exists(icoPath)) {
                    using var ico = new Icon(icoPath, 32, 32);
                    using var bmp = ico.ToBitmap();
                    return bmp.GetHicon();
                }

                string pngPath = Path.Combine(baseDir, "PanakLogo.png");
                if (!File.Exists(pngPath)) {
                    pngPath = Path.Combine(baseDir, "Panak logo.png");
                }
                if (File.Exists(pngPath)) {
                    using var orig = new Bitmap(pngPath);
                    using var resized = new Bitmap(orig, new System.Drawing.Size(32, 32));
                    return resized.GetHicon();
                }

                // Check resource stream
                var uri = new Uri("pack://application:,,,/PanakLogo.png", UriKind.Absolute);
                var sri = Application.GetResourceStream(uri);
                if (sri != null) {
                    using var stream = sri.Stream;
                    using var bmp = new Bitmap(stream);
                    using var resized = new Bitmap(bmp, new System.Drawing.Size(32, 32));
                    return resized.GetHicon();
                }
            } catch { }

            // Fallback: render plate
            using (Bitmap fallbackBmp = IconGenerator.RenderPlateWithGrid(32, "Squircle", System.Drawing.Color.FromArgb(59, 130, 246), new System.Collections.Generic.List<Bitmap>())) {
                return fallbackBmp.GetHicon();
            }
        }

        private void AddOrUpdateTrayIcon() {
            if (_hwndSource == null) return;
            if (_hIcon == IntPtr.Zero) {
                _hIcon = LoadPanakTrayIcon();
            }

            NOTIFYICONDATA nid = new NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwndSource.Handle,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "Panak - Android-style popup folders"
            };

            if (_isAdded) {
                Shell_NotifyIcon(NIM_MODIFY, ref nid);
            } else {
                _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
                if (!_isAdded) {
                    // Taskbar notification area may still be starting up on Windows login. Retry in background.
                    System.Threading.Tasks.Task.Run(async () => {
                        for (int i = 0; i < 10 && !_isAdded; i++) {
                            await System.Threading.Tasks.Task.Delay(1500);
                            Application.Current?.Dispatcher?.Invoke(() => {
                                if (!_isAdded && _hwndSource != null) {
                                    _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
                                }
                            });
                        }
                    });
                }
            }
        }

        private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (_wmTaskbarCreated != 0 && msg == _wmTaskbarCreated) {
                _isAdded = false;
                AddOrUpdateTrayIcon();
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == WM_TRAYICON) {
                int mouseMsg = lParam.ToInt32();
                if (mouseMsg == WM_LBUTTONUP) {
                    Application.Current.Dispatcher.Invoke(() => {
                        SettingsRequested?.Invoke();
                    });
                    handled = true;
                } else if (mouseMsg == WM_RBUTTONUP) {
                    Application.Current.Dispatcher.Invoke(ShowContextMenu);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void ShowContextMenu() {
            var loc = LocalizationService.Instance;
            var settings = SettingsService.Instance.Current;

            bool isDark = (settings.ThemeMode == "Dark") || (settings.ThemeMode != "Light" && ShellHelper.IsWindowsDarkTheme());
            string themeColor = string.IsNullOrEmpty(settings.ThemeColor) ? ShellHelper.GetDefaultPastelColorHex() : settings.ThemeColor;
            double hue = ShellHelper.GetHueFromHex(themeColor);

            System.Windows.Media.Color bgSurface, fgOnSurface, primaryColor, borderCol, hoverBg, sepCol;
            if (isDark) {
                bgSurface = System.Windows.Media.Color.FromArgb(242, 28, 27, 34);
                fgOnSurface = System.Windows.Media.Color.FromRgb(248, 250, 252);
                var pCol = ShellHelper.ColorFromHsl(hue, 0.70, 0.72);
                primaryColor = System.Windows.Media.Color.FromRgb(pCol.R, pCol.G, pCol.B);
                borderCol = System.Windows.Media.Color.FromArgb(45, 255, 255, 255);
                hoverBg = System.Windows.Media.Color.FromArgb(50, primaryColor.R, primaryColor.G, primaryColor.B);
                sepCol = System.Windows.Media.Color.FromArgb(30, 255, 255, 255);
            } else {
                var (sCol, cCol, pCol) = ShellHelper.GetMaterialYouTonalPalette(hue);
                bgSurface = System.Windows.Media.Color.FromArgb(248, sCol.R, sCol.G, sCol.B);
                fgOnSurface = System.Windows.Media.Color.FromRgb(30, 41, 59);
                primaryColor = System.Windows.Media.Color.FromRgb(pCol.R, pCol.G, pCol.B);
                borderCol = System.Windows.Media.Color.FromArgb(50, 0, 0, 0);
                hoverBg = System.Windows.Media.Color.FromArgb(40, primaryColor.R, primaryColor.G, primaryColor.B);
                sepCol = System.Windows.Media.Color.FromArgb(25, 0, 0, 0);
            }

            ContextMenu menu = new ContextMenu {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Placement = PlacementMode.MousePoint,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };

            // Custom ContextMenu Template with rounded Card & DropShadow
            var menuTemplate = new ControlTemplate(typeof(ContextMenu));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(bgSurface));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(borderCol));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(5));
            borderFactory.SetValue(Border.MinWidthProperty, 175.0);
            borderFactory.SetValue(UIElement.EffectProperty, new DropShadowEffect {
                BlurRadius = 18,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = 0.30,
                Color = System.Windows.Media.Colors.Black
            });

            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.IsItemsHostProperty, true);
            borderFactory.AppendChild(panelFactory);
            menuTemplate.VisualTree = borderFactory;
            menu.Template = menuTemplate;

            MenuItem CreateItem(string icon, string header, RoutedEventHandler onClick) {
                MenuItem item = new MenuItem {
                    Header = header,
                    Cursor = Cursors.Hand,
                    Height = 34
                };

                var itemTemplate = new ControlTemplate(typeof(MenuItem));
                var itemBorder = new FrameworkElementFactory(typeof(Border), "ItemBorder");
                itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
                itemBorder.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                itemBorder.SetValue(Border.PaddingProperty, new Thickness(10, 0, 12, 0));

                var grid = new FrameworkElementFactory(typeof(Grid));
                var colIcon = new FrameworkElementFactory(typeof(ColumnDefinition));
                colIcon.SetValue(ColumnDefinition.WidthProperty, new GridLength(24));
                var colText = new FrameworkElementFactory(typeof(ColumnDefinition));
                colText.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
                grid.AppendChild(colIcon);
                grid.AppendChild(colText);

                var iconBlock = new FrameworkElementFactory(typeof(TextBlock));
                iconBlock.SetValue(TextBlock.TextProperty, icon);
                iconBlock.SetValue(TextBlock.FontSizeProperty, 12.0);
                iconBlock.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                iconBlock.SetValue(Grid.ColumnProperty, 0);
                grid.AppendChild(iconBlock);

                var headerBlock = new FrameworkElementFactory(typeof(TextBlock), "HeaderText");
                headerBlock.SetValue(TextBlock.TextProperty, header);
                headerBlock.SetValue(TextBlock.FontSizeProperty, 12.0);
                headerBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
                headerBlock.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(fgOnSurface));
                headerBlock.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                headerBlock.SetValue(Grid.ColumnProperty, 1);
                grid.AppendChild(headerBlock);

                itemBorder.AppendChild(grid);
                itemTemplate.VisualTree = itemBorder;

                var trigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "ItemBorder"));
                trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(primaryColor), "HeaderText"));
                itemTemplate.Triggers.Add(trigger);

                item.Template = itemTemplate;
                item.Click += onClick;
                return item;
            }

            Separator CreateSeparator() {
                Separator sep = new Separator {
                    Height = 1,
                    Margin = new Thickness(6, 4, 6, 4)
                };
                var sepTemplate = new ControlTemplate(typeof(Separator));
                var sepBorder = new FrameworkElementFactory(typeof(Border));
                sepBorder.SetValue(Border.HeightProperty, 1.0);
                sepBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(sepCol));
                sepTemplate.VisualTree = sepBorder;
                sep.Template = sepTemplate;
                return sep;
            }

            menu.Items.Add(CreateItem("⚙️", loc["Settings"], (s, e) => SettingsRequested?.Invoke()));
            menu.Items.Add(CreateItem("📁", loc["NewFolder"], (s, e) => NewFolderRequested?.Invoke()));
            menu.Items.Add(CreateItem("🔄", loc["CheckForUpdates"], async (s, e) => {
                bool found = await GitHubUpdateService.Instance.CheckForUpdatesAsync(manual: true);
                if (found) {
                    ShowBalloon(loc["UpdateAvailable"], $"{loc["LatestVersion"]}: v{GitHubUpdateService.Instance.LatestVersionString}");
                } else if (GitHubUpdateService.Instance.Status == UpdateStatus.UpToDate) {
                    ShowBalloon(loc["Updates"], loc["UpToDate"]);
                } else if (GitHubUpdateService.Instance.Status == UpdateStatus.Error) {
                    ShowBalloon(loc["Updates"], loc["UpdateError"]);
                }
            }));

            menu.Items.Add(CreateSeparator());

            menu.Items.Add(CreateItem("✕", loc["Exit"], (s, e) => ExitRequested?.Invoke()));

            if (_hwndSource != null) {
                ShellHelper.SetForegroundWindow(_hwndSource.Handle);
            }
            menu.IsOpen = true;
        }

        public void ShowBalloon(string title, string message) {
            if (!_isAdded || _hwndSource == null) return;
            NOTIFYICONDATA nid = new NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwndSource.Handle,
                uID = 1001,
                uFlags = NIF_INFO,
                szInfoTitle = title ?? "",
                szInfo = message ?? "",
                dwInfoFlags = NIIF_INFO
            };
            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }

        public void Dispose() {
            if (_isAdded && _hwndSource != null) {
                NOTIFYICONDATA nid = new NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwndSource.Handle,
                    uID = 1001
                };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
                _isAdded = false;
            }

            if (_hIcon != IntPtr.Zero) {
                ShellHelper.DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            _hwndSource?.Dispose();
            _hwndSource = null;
        }
    }
}
