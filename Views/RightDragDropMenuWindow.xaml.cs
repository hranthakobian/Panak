using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Panak.Services;

namespace Panak.Views {
    public partial class RightDragDropMenuWindow : Window {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WM_CLOSE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EndMenu();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
            if (IntPtr.Size == 8) {
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size == 8) {
                return GetWindowLongPtr64(hWnd, nIndex);
            }
            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private List<string> _files = new List<string>();
        private string _targetDir = string.Empty;
        private Point _dropPoint;
        private IntPtr _hMenuWnd = IntPtr.Zero;
        private DispatcherTimer? _monitorTimer;
        private int _checksCount = 0;

        public Action<List<string>, string, Point>? CollectAction { get; set; }

        public RightDragDropMenuWindow() {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            IntPtr curExStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            IntPtr newExStyle = new IntPtr(curExStyle.ToInt64() | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, newExStyle);
        }

        public void ShowMenu(List<string> files, string targetDir, Point dropPoint) {
            _files = files;
            _targetDir = targetDir;
            _dropPoint = dropPoint;

            string baseText = LocalizationService.Instance["CollectIntoPanakFolder"];
            ActionLabelText.Text = files.Count > 1 ? $"{baseText} ({files.Count})" : baseText;

            // Initial positioning
            PositionNearPoint(dropPoint);
            Show();

            // Check for Explorer context menu window (#32768) and align with it
            _monitorTimer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
        }

        private void PositionNearPoint(Point pt) {
            Rect workArea = SystemParameters.WorkArea;
            double x = pt.X;
            double y = pt.Y - 50;

            if (y < workArea.Top + 10) {
                y = pt.Y + 20;
            }
            if (x + 240 > workArea.Right) {
                x = workArea.Right - 250;
            }
            if (x < workArea.Left) {
                x = workArea.Left + 10;
            }

            Left = x;
            Top = y;
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e) {
            _checksCount++;

            if (_hMenuWnd == IntPtr.Zero) {
                IntPtr hwnd = FindWindow("#32768", null);
                if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd)) {
                    _hMenuWnd = hwnd;
                    AlignWithMenu(hwnd);
                }
            } else {
                if (!IsWindow(_hMenuWnd) || !IsWindowVisible(_hMenuWnd)) {
                    // Menu closed by user selecting another option or clicking away
                    CloseWindow();
                    return;
                }
            }

            // Auto-close after 8 seconds if abandoned
            if (_checksCount > 160) {
                CloseWindow();
            }
        }

        private void AlignWithMenu(IntPtr hwnd) {
            try {
                if (GetWindowRect(hwnd, out RECT rect)) {
                    Rect workArea = SystemParameters.WorkArea;
                    double menuLeft = rect.Left;
                    double menuTop = rect.Top;
                    double menuBottom = rect.Bottom;

                    double myHeight = ActualHeight > 0 ? ActualHeight : 44;
                    double targetY = menuTop - myHeight - 2;

                    if (targetY < workArea.Top + 10) {
                        targetY = menuBottom + 4;
                    }

                    double targetX = menuLeft;
                    if (targetX + ActualWidth > workArea.Right) {
                        targetX = workArea.Right - ActualWidth - 10;
                    }

                    Left = targetX;
                    Top = targetY;
                }
            } catch {
                // Ignore positioning errors
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e) {
            if (_hMenuWnd != IntPtr.Zero && IsWindow(_hMenuWnd)) {
                PostMessage(_hMenuWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                EndMenu();
            }

            var files = new List<string>(_files);
            string targetDir = _targetDir;
            Point dropPoint = _dropPoint;

            CloseWindow();
            CollectAction?.Invoke(files, targetDir, dropPoint);
        }

        private void CloseWindow() {
            _monitorTimer?.Stop();
            _monitorTimer = null;
            Close();
        }
    }
}
