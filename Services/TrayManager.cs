using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Panak.Views;

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
            using (Bitmap fallbackBmp = IconGenerator.RenderPlateWithGrid(32, "Squircle", Color.FromArgb(59, 130, 246), new System.Collections.Generic.List<Bitmap>())) {
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

            ContextMenu menu = new ContextMenu();

            MenuItem mSettings = new MenuItem { Header = loc["Settings"] };
            mSettings.Click += (s, e) => SettingsRequested?.Invoke();
            menu.Items.Add(mSettings);

            MenuItem mNew = new MenuItem { Header = loc["NewFolder"] };
            mNew.Click += (s, e) => NewFolderRequested?.Invoke();
            menu.Items.Add(mNew);

            MenuItem mUpdate = new MenuItem { Header = loc["CheckForUpdates"] };
            mUpdate.Click += async (s, e) => {
                bool found = await GitHubUpdateService.Instance.CheckForUpdatesAsync(manual: true);
                if (found) {
                    ShowBalloon(loc["UpdateAvailable"], $"{loc["LatestVersion"]}: v{GitHubUpdateService.Instance.LatestVersionString}");
                } else if (GitHubUpdateService.Instance.Status == UpdateStatus.UpToDate) {
                    ShowBalloon(loc["Updates"], loc["UpToDate"]);
                } else if (GitHubUpdateService.Instance.Status == UpdateStatus.Error) {
                    ShowBalloon(loc["Updates"], loc["UpdateError"]);
                }
            };
            menu.Items.Add(mUpdate);

            menu.Items.Add(new Separator());

            MenuItem mExit = new MenuItem { Header = loc["Exit"] };
            mExit.Click += (s, e) => ExitRequested?.Invoke();
            menu.Items.Add(mExit);

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
