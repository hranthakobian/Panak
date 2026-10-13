using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Panak.Services {
    public class HookService : IDisposable {
        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;

        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_KEYDOWN = 0x0100;

        private const int VK_SPACE = 0x20;
        private const int VK_ESCAPE = 0x1B;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_SHIFT_KEY = 0x10;
        private const int VK_CONTROL_KEY = 0x11;
        private const int VK_MENU_KEY = 0x12; // Alt

        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;

        // Keep references alive to avoid Garbage Collection
        private readonly LowLevelProc _mouseProc;
        private readonly LowLevelProc _keyboardProc;

        private DateTime _lastRightUp = DateTime.MinValue;
        private POINT _lastRightUpPt;

        private DateTime _lastLeftDown = DateTime.MinValue;
        private POINT _lastLeftDownPt;
        private bool _suppressNextLeftUp = false;

        // Left drag & drop (file onto file)
        private ExplorerHelper.ItemInfo? _leftDragSourceItem;
        private List<string>? _leftDragSelectedFiles;
        private POINT _leftDragStartPt;
        private bool _isLeftDragging = false;

        public event Action<ExplorerHelper.ItemInfo>? LeftDoubleClickTriggered;
        public event Action<ExplorerHelper.ItemInfo>? RightDoubleClickTriggered;
        public event Action<ExplorerHelper.ItemInfo>? SpaceQuickLookTriggered;
        public event Action? CloseRequested;
        public event Action<List<string>, string, Rect>? FileDroppedOnOtherFile;

        public Func<bool>? IsPopupOpenFunc { get; set; }
        public Func<bool>? IsPopupWithinGracePeriodFunc { get; set; }
        public Func<IntPtr>? GetPopupHwndFunc { get; set; }

        public HookService() {
            _mouseProc = MouseHookCallback;
            _keyboardProc = KeyboardHookCallback;
        }

        public void Start() {
            if (_mouseHook == IntPtr.Zero) {
                using Process curProcess = Process.GetCurrentProcess();
                using ProcessModule curModule = curProcess.MainModule!;
                IntPtr hMod = GetModuleHandle(curModule.ModuleName);
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
                _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            }
        }

        public void Stop() {
            if (_mouseHook != IntPtr.Zero) {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (_keyboardHook != IntPtr.Zero) {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                try {
                    int msg = wParam.ToInt32();

                // Detect click outside active folder popup
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN) {
                    if (IsPopupOpenFunc?.Invoke() == true) {
                        if (IsPopupWithinGracePeriodFunc?.Invoke() != true) {
                            IntPtr popupHwnd = GetPopupHwndFunc?.Invoke() ?? IntPtr.Zero;
                            if (popupHwnd != IntPtr.Zero) {
                                MSLLHOOKSTRUCT hs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                                ShellHelper.POINT pt = new ShellHelper.POINT { X = hs.pt.x, Y = hs.pt.y };
                                IntPtr clickedHwnd = ShellHelper.WindowFromPoint(pt);
                                IntPtr clickedRoot = ShellHelper.GetAncestor(clickedHwnd, ShellHelper.GA_ROOT);

                                if (clickedHwnd != popupHwnd && clickedRoot != popupHwnd) {
                                    Application.Current.Dispatcher.BeginInvoke(() => {
                                        CloseRequested?.Invoke();
                                    });
                                }
                            }
                        }
                    }
                }

                if (msg == WM_MOUSEMOVE) {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (!_isLeftDragging && _leftDragStartPt.x != 0 && _leftDragStartPt.y != 0) {
                        int dx = Math.Abs(hookStruct.pt.x - _leftDragStartPt.x);
                        int dy = Math.Abs(hookStruct.pt.y - _leftDragStartPt.y);
                        if (dx > 8 || dy > 8) {
                            _isLeftDragging = true;
                            if (_leftDragSourceItem == null) {
                                var curItem = ExplorerHelper.GetItemUnderCursor();
                                if (curItem != null && !string.IsNullOrEmpty(curItem.Value.Path) && !curItem.Value.IsFolder && File.Exists(curItem.Value.Path)) {
                                    _leftDragSourceItem = curItem.Value;
                                    _leftDragSelectedFiles = ExplorerHelper.GetActiveExplorerSelectedPaths();
                                }
                            }
                        }
                    }
                } else if (msg == WM_LBUTTONDOWN) {
                    if (_suppressNextLeftUp) {
                        _suppressNextLeftUp = false;
                    }

                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    // Track potential drag source item
                    _leftDragStartPt = hookStruct.pt;
                    _isLeftDragging = false;
                    _leftDragSourceItem = null;
                    _leftDragSelectedFiles = null;

                    uint doubleClickTime = ShellHelper.GetDoubleClickTime();
                    if (doubleClickTime == 0) doubleClickTime = 500;
                    int maxDeltaX = ShellHelper.GetSystemMetrics(ShellHelper.SM_CXDOUBLECLK);
                    if (maxDeltaX <= 0) maxDeltaX = 4;
                    int maxDeltaY = ShellHelper.GetSystemMetrics(ShellHelper.SM_CYDOUBLECLK);
                    if (maxDeltaY <= 0) maxDeltaY = 4;

                    TimeSpan elapsed = DateTime.Now - _lastLeftDown;
                    int dx = Math.Abs(hookStruct.pt.x - _lastLeftDownPt.x);
                    int dy = Math.Abs(hookStruct.pt.y - _lastLeftDownPt.y);

                    if (elapsed.TotalMilliseconds <= doubleClickTime && dx <= maxDeltaX && dy <= maxDeltaY) {
                        ExplorerHelper.ItemInfo? item = ExplorerHelper.GetSelectedItemInActiveExplorer();
                        if (item == null || string.IsNullOrEmpty(item.Value.Path) || !PanakManager.Instance.IsPanakFolder(item.Value.Path)) {
                            item = ExplorerHelper.GetItemUnderCursor();
                        }

                        if (item != null && !string.IsNullOrEmpty(item.Value.Path) && PanakManager.Instance.IsPanakFolder(item.Value.Path)) {
                            _lastLeftDown = DateTime.MinValue;
                            _suppressNextLeftUp = true;
                            var targetItem = item.Value;
                            Application.Current.Dispatcher.BeginInvoke(() => {
                                LeftDoubleClickTriggered?.Invoke(targetItem);
                            });
                            return (IntPtr)1; // Swallow second click so Windows Explorer does not open the folder!
                        } else {
                            _lastLeftDown = DateTime.Now;
                            _lastLeftDownPt = hookStruct.pt;
                        }
                    } else {
                        _lastLeftDown = DateTime.Now;
                        _lastLeftDownPt = hookStruct.pt;
                    }
                } else if (msg == WM_LBUTTONUP) {
                    if (_suppressNextLeftUp) {
                        _suppressNextLeftUp = false;
                        return (IntPtr)1; // Swallow mouse up
                    }

                    var srcItem = _leftDragSourceItem;
                    if (_isLeftDragging && srcItem.HasValue && !string.IsNullOrEmpty(srcItem.Value.Path)) {
                        if (SettingsService.Instance.Current.CombineFilesOnDrop) {
                            var dropTarget = ExplorerHelper.GetItemUnderCursor();
                            if (dropTarget != null && !string.IsNullOrEmpty(dropTarget.Value.Path) && !dropTarget.Value.IsFolder && File.Exists(dropTarget.Value.Path)) {
                                if (!string.Equals(dropTarget.Value.Path, srcItem.Value.Path, StringComparison.OrdinalIgnoreCase)) {
                                    var files = new List<string>();
                                    if (_leftDragSelectedFiles != null && _leftDragSelectedFiles.Contains(srcItem.Value.Path, StringComparer.OrdinalIgnoreCase)) {
                                        files.AddRange(_leftDragSelectedFiles);
                                    } else {
                                        files.Add(srcItem.Value.Path);
                                    }
                                    if (!files.Contains(dropTarget.Value.Path, StringComparer.OrdinalIgnoreCase)) {
                                        files.Add(dropTarget.Value.Path);
                                    }
                                    var targetPath = dropTarget.Value.Path;
                                    var targetBounds = dropTarget.Value.BoundingBox;
                                    Application.Current.Dispatcher.BeginInvoke(() => {
                                        FileDroppedOnOtherFile?.Invoke(files, targetPath, targetBounds);
                                    });
                                }
                            }
                        }
                        _isLeftDragging = false;
                        _leftDragSourceItem = null;
                        _leftDragSelectedFiles = null;
                    }
                } else if (msg == WM_RBUTTONUP) {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (SettingsService.Instance.Current.RightDoubleClickEnabled) {
                        uint doubleClickTime = ShellHelper.GetDoubleClickTime();
                        int maxDeltaX = ShellHelper.GetSystemMetrics(ShellHelper.SM_CXDOUBLECLK);
                        int maxDeltaY = ShellHelper.GetSystemMetrics(ShellHelper.SM_CYDOUBLECLK);

                        TimeSpan elapsed = DateTime.Now - _lastRightUp;
                        int dx = Math.Abs(hookStruct.pt.x - _lastRightUpPt.x);
                        int dy = Math.Abs(hookStruct.pt.y - _lastRightUpPt.y);

                        if (elapsed.TotalMilliseconds <= doubleClickTime && dx <= maxDeltaX && dy <= maxDeltaY) {
                            _lastRightUp = DateTime.MinValue;

                            var item = ExplorerHelper.GetItemUnderCursor();
                            if (item != null && !string.IsNullOrEmpty(item.Value.Path)) {
                                Application.Current.Dispatcher.Invoke(() => {
                                    RightDoubleClickTriggered?.Invoke(item.Value);
                                });
                                return (IntPtr)1; // Suppress context menu
                            }
                        } else {
                            _lastRightUp = DateTime.Now;
                            _lastRightUpPt = hookStruct.pt;
                        }
                    }
                }
            } catch { }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

        public static bool MatchesHotkey(string? hotkeyString, uint vkCode) {
            if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

            var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            bool reqCtrl = false;
            bool reqAlt = false;
            bool reqShift = false;
            string mainKeyName = "";

            foreach (var part in parts) {
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) {
                    reqCtrl = true;
                } else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) {
                    reqAlt = true;
                } else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) {
                    reqShift = true;
                } else {
                    mainKeyName = part;
                }
            }

            if (string.IsNullOrEmpty(mainKeyName)) {
                return false;
            }

            // Check modifiers
            bool isCtrlPressed = (GetAsyncKeyState(VK_CONTROL_KEY) & 0x8000) != 0;
            bool isAltPressed = (GetAsyncKeyState(VK_MENU_KEY) & 0x8000) != 0;
            bool isShiftPressed = (GetAsyncKeyState(VK_SHIFT_KEY) & 0x8000) != 0;

            if (reqCtrl != isCtrlPressed) return false;
            if (reqAlt != isAltPressed) return false;
            if (reqShift != isShiftPressed) return false;

            // Check main key
            uint targetVk = ConvertKeyNameToVk(mainKeyName);
            return vkCode == targetVk;
        }

        public static uint ConvertKeyNameToVk(string keyName) {
            if (string.IsNullOrWhiteSpace(keyName)) return 0;
            string norm = keyName.Trim();

            if (norm.Equals("Space", StringComparison.OrdinalIgnoreCase) || norm.Equals("␣", StringComparison.OrdinalIgnoreCase)) return 0x20;
            if (norm.Equals("Escape", StringComparison.OrdinalIgnoreCase) || norm.Equals("Esc", StringComparison.OrdinalIgnoreCase)) return 0x1B;
            if (norm.Equals("Tab", StringComparison.OrdinalIgnoreCase)) return 0x09;
            if (norm.Equals("Return", StringComparison.OrdinalIgnoreCase) || norm.Equals("Enter", StringComparison.OrdinalIgnoreCase)) return 0x0D;
            if (norm.Equals("Back", StringComparison.OrdinalIgnoreCase) || norm.Equals("Backspace", StringComparison.OrdinalIgnoreCase)) return 0x08;

            if (Enum.TryParse<System.Windows.Input.Key>(norm, true, out var wpfKey)) {
                int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(wpfKey);
                if (vk > 0) return (uint)vk;
            }

            if (norm.Length == 1) {
                char ch = char.ToUpperInvariant(norm[0]);
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) {
                    return (uint)ch;
                }
            }

            return 0;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && wParam.ToInt32() == WM_KEYDOWN) {
                try {
                    KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    bool isPopupOpen = IsPopupOpenFunc?.Invoke() ?? false;

                    string closeHotkey = SettingsService.Instance.Current.ClosePopupHotkey ?? "Escape";
                    string quickLookHotkey = SettingsService.Instance.Current.QuickLookHotkey ?? "Space";

                    if (isPopupOpen && MatchesHotkey(closeHotkey, hookStruct.vkCode)) {
                        Application.Current.Dispatcher.Invoke(() => {
                            CloseRequested?.Invoke();
                        });
                        return (IntPtr)1;
                    }

                    if (MatchesHotkey(quickLookHotkey, hookStruct.vkCode)) {
                        if (isPopupOpen) {
                            Application.Current.Dispatcher.Invoke(() => {
                                CloseRequested?.Invoke();
                            });
                            return (IntPtr)1;
                        }

                        if (SettingsService.Instance.Current.SpaceQuickLookEnabled) {
                            if (!ExplorerHelper.IsFocusInsideTextEdit()) {
                                var item = ExplorerHelper.GetSelectedItemInActiveExplorer();
                                if (item != null && !string.IsNullOrEmpty(item.Value.Path)) {
                                    Application.Current.Dispatcher.Invoke(() => {
                                        SpaceQuickLookTriggered?.Invoke(item.Value);
                                    });
                                    return (IntPtr)1; // Swallow key
                                }
                            }
                        }
                    }

                    // Enter key on Panak folder in Windows Explorer or Desktop
                    if (hookStruct.vkCode == 0x0D) { // VK_RETURN
                        if (!isPopupOpen && !ExplorerHelper.IsFocusInsideTextEdit()) {
                            var item = ExplorerHelper.GetSelectedItemInActiveExplorer();
                            if (item != null && !string.IsNullOrEmpty(item.Value.Path) && PanakManager.Instance.IsPanakFolder(item.Value.Path)) {
                                var targetItem = item.Value;
                                Application.Current.Dispatcher.Invoke(() => {
                                    LeftDoubleClickTriggered?.Invoke(targetItem);
                                });
                                return (IntPtr)1; // Swallow Enter key so Windows Explorer does not open the folder!
                            }
                        }
                    }
                } catch { }
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        public void Dispose() {
            Stop();
        }
    }
}
