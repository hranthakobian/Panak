using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Panak.Services {
    public static class ShellHelper {
        public const uint SHCNE_UPDATEITEM = 0x00002000;
        public const uint SHCNE_UPDATEDIR = 0x00001000;
        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_PATHW = 0x0005;
        public const uint SHCNF_FLUSH = 0x1000;
        public const uint SHCNF_FLUSHNOWAIT = 0x2000;

        public const uint WM_COMMAND = 0x0111;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const int VK_F5 = 0x74;
        public const int CMD_SHELL_REFRESH_1 = 28931; // 0x7103
        public const int CMD_SHELL_REFRESH_2 = 41504; // 0xA220

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateWindow(IntPtr hWnd);

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFOLDERCUSTOMSETTINGS {
            public uint dwSize;
            public uint dwMask;
            public IntPtr pvid;
            public string pszWebViewTemplate;
            public uint cchWebViewTemplate;
            public string pszWebViewTemplateVersion;
            public string pszInfoTip;
            public uint cchInfoTip;
            public IntPtr pclsid;
            public uint dwFlags;
            public string pszIconFile;
            public uint cchIconFile;
            public int iIconIndex;
            public string pszLogo;
            public uint cchLogo;
        }

        public const uint FCSM_ICONFILE = 0x00000010;
        public const uint FCS_FORCEWRITE = 0x00000002;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern int SHGetSetFolderCustomSettings(ref SHFOLDERCUSTOMSETTINGS pfcs, string pszPath, uint dwReadWrite);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public const uint WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public static void ForceForegroundWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return;
            }
            try {
                IntPtr fgHwnd = GetForegroundWindow();
                uint fgThread = GetWindowThreadProcessId(fgHwnd, out _);
                uint currentThread = GetCurrentThreadId();

                if (fgThread != 0 && fgThread != currentThread) {
                    AttachThreadInput(currentThread, fgThread, true);
                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                    SetActiveWindow(hWnd);
                    AttachThreadInput(currentThread, fgThread, false);
                } else {
                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                    SetActiveWindow(hWnd);
                }
            } catch {
                SetForegroundWindow(hWnd);
            }
        }

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        public const uint GA_ROOT = 2;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLong(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            return GetWindowLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        public const int SM_CXDOUBLECLK = 36;
        public const int SM_CYDOUBLECLK = 37;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        public const uint SHCNE_MKDIR = 0x00000008;
        public const uint SHCNE_CREATE = 0x00000002;
        public const uint SHCNE_DELETE = 0x00000004;
        public const uint SHCNE_RMDIR = 0x00000010;
        public const uint SHCNE_RENAMEFOLDER = 0x00020000;

        public static void NotifyFolderRenamed(string oldPath, string newPath) {
            try {
                IntPtr pOld = Marshal.StringToHGlobalUni(oldPath);
                IntPtr pNew = Marshal.StringToHGlobalUni(newPath);
                string parentOld = Path.GetDirectoryName(oldPath) ?? string.Empty;
                string parentNew = Path.GetDirectoryName(newPath) ?? string.Empty;
                IntPtr pParentOld = !string.IsNullOrEmpty(parentOld) ? Marshal.StringToHGlobalUni(parentOld) : IntPtr.Zero;
                IntPtr pParentNew = !string.IsNullOrEmpty(parentNew) ? Marshal.StringToHGlobalUni(parentNew) : IntPtr.Zero;

                SHChangeNotify(SHCNE_RENAMEFOLDER, SHCNF_PATHW, pOld, pNew);
                if (pParentOld != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParentOld, IntPtr.Zero);
                }
                if (pParentNew != IntPtr.Zero && pParentNew != pParentOld) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParentNew, IntPtr.Zero);
                }

                Marshal.FreeHGlobal(pOld);
                Marshal.FreeHGlobal(pNew);
                if (pParentOld != IntPtr.Zero) Marshal.FreeHGlobal(pParentOld);
                if (pParentNew != IntPtr.Zero && pParentNew != pParentOld) Marshal.FreeHGlobal(pParentNew);
            } catch {
                // Ignore notification failure
            }
        }

        public static void NotifyFolderIconChanged(string folderPath, string? iconFilePath = null) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                // Ensure folder ReadOnly attribute is set so Windows Explorer honors desktop.ini
                try {
                    var folderAttrs = File.GetAttributes(folderPath);
                    if (!folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);
                    }
                } catch { }

                // Force Windows Shell to register the new custom folder icon settings immediately
                if (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath)) {
                    try {
                        var fcs = new SHFOLDERCUSTOMSETTINGS();
                        fcs.dwSize = (uint)Marshal.SizeOf(typeof(SHFOLDERCUSTOMSETTINGS));
                        fcs.dwMask = FCSM_ICONFILE;
                        fcs.pszIconFile = Path.GetFileName(iconFilePath);
                        fcs.iIconIndex = 0;
                        SHGetSetFolderCustomSettings(ref fcs, folderPath, FCS_FORCEWRITE);
                    } catch { }
                }

                string desktopIni = Path.Combine(folderPath, "desktop.ini");
                IntPtr pIni = File.Exists(desktopIni) ? Marshal.StringToHGlobalUni(desktopIni) : IntPtr.Zero;
                IntPtr pIco = (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath)) ? Marshal.StringToHGlobalUni(iconFilePath) : IntPtr.Zero;
                IntPtr pPath = Marshal.StringToHGlobalUni(folderPath);
                string parent = Path.GetDirectoryName(folderPath) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                uint notifyFlags = SHCNF_PATHW | SHCNF_FLUSHNOWAIT;

                if (pIco != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pIco, IntPtr.Zero);
                }

                if (pIni != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pIni, IntPtr.Zero);
                }

                SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pPath, IntPtr.Zero);
                SHChangeNotify(SHCNE_UPDATEDIR, notifyFlags, pPath, IntPtr.Zero);

                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pParent, IntPtr.Zero);
                    SHChangeNotify(SHCNE_UPDATEDIR, notifyFlags, pParent, IntPtr.Zero);
                }

                if (pIco != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pIco);
                }
                if (pIni != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pIni);
                }
                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }

                // Trigger instant visual refresh on Desktop and Explorer windows in real-time
                RefreshDesktopAndExplorerViews();
            } catch {
                // Ignore notification failure
            }
        }

        public static (IntPtr defView, IntPtr listView) GetDesktopHandles() {
            try {
                IntPtr progman = FindWindow("Progman", null);
                IntPtr defView = IntPtr.Zero;
                if (progman != IntPtr.Zero) {
                    defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                }

                if (defView == IntPtr.Zero) {
                    IntPtr workerW = IntPtr.Zero;
                    while ((workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero) {
                        defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (defView != IntPtr.Zero) break;
                    }
                }

                if (defView == IntPtr.Zero) {
                    try {
                        IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
                        if (hDesk != IntPtr.Zero) {
                            SetThreadDesktop(hDesk);
                            progman = FindWindow("Progman", null);
                            if (progman != IntPtr.Zero) {
                                defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                            }
                            if (defView == IntPtr.Zero) {
                                IntPtr workerW = IntPtr.Zero;
                                while ((workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero) {
                                    defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                                    if (defView != IntPtr.Zero) break;
                                }
                            }
                        }
                    } catch { }
                }

                IntPtr listView = IntPtr.Zero;
                if (defView != IntPtr.Zero) {
                    listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                }

                return (defView, listView);
            } catch {
                return (IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static IntPtr GetDesktopDefViewHandle() {
            return GetDesktopHandles().defView;
        }

        public static void RefreshDesktopAndExplorerViews() {
            try {
                var (defView, listView) = GetDesktopHandles();
                if (defView != IntPtr.Zero) {
                    PostMessage(defView, WM_COMMAND, (IntPtr)CMD_SHELL_REFRESH_1, IntPtr.Zero);
                    PostMessage(defView, WM_COMMAND, (IntPtr)CMD_SHELL_REFRESH_2, IntPtr.Zero);

                    if (listView != IntPtr.Zero) {
                        PostMessage(listView, WM_KEYDOWN, (IntPtr)VK_F5, IntPtr.Zero);
                        PostMessage(listView, WM_KEYUP, (IntPtr)VK_F5, (IntPtr)unchecked((int)0xC0000001));
                        InvalidateRect(listView, IntPtr.Zero, true);
                        UpdateWindow(listView);
                    }

                    PostMessage(defView, WM_KEYDOWN, (IntPtr)VK_F5, IntPtr.Zero);
                    PostMessage(defView, WM_KEYUP, (IntPtr)VK_F5, (IntPtr)unchecked((int)0xC0000001));
                    InvalidateRect(defView, IntPtr.Zero, true);
                    UpdateWindow(defView);
                }

                System.Threading.Tasks.Task.Run(() => {
                    try {
                        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                        if (shellType != null) {
                            dynamic? shell = Activator.CreateInstance(shellType);
                            if (shell != null) {
                                dynamic windows = shell.Windows();
                                int count = windows.Count;
                                for (int i = 0; i < count; i++) {
                                    try {
                                        dynamic item = windows.Item(i);
                                        item?.Document?.Refresh();
                                    } catch { }
                                }
                            }
                        }
                    } catch { }
                });
            } catch { }
        }

        public static void NotifyShellItemChanged(string path) {
            try {
                IntPtr pPath = Marshal.StringToHGlobalUni(path);
                string parent = Path.GetDirectoryName(path) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pPath, IntPtr.Zero);
                SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pPath, IntPtr.Zero);
                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParent, IntPtr.Zero);
                    SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pParent, IntPtr.Zero);
                }

                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }
            } catch {
                // Ignore notification failure
            }
        }

        public static void NotifyFolderCreated(string folderPath) {
            try {
                IntPtr pPath = Marshal.StringToHGlobalUni(folderPath);
                string parent = Path.GetDirectoryName(folderPath) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                SHChangeNotify(SHCNE_MKDIR, SHCNF_PATHW, pPath, IntPtr.Zero);
                SHChangeNotify(SHCNE_CREATE, SHCNF_PATHW, pPath, IntPtr.Zero);
                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParent, IntPtr.Zero);
                }
                SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pPath, IntPtr.Zero);

                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }
            } catch {
                // Fallback
            }
        }

        public static void NotifyShellAssocChanged() {
            try {
                SHChangeNotify(SHCNE_ASSOCCHANGED, 0 /* SHCNF_IDLIST */, IntPtr.Zero, IntPtr.Zero);
            } catch {
                // Ignore notification failure
            }
        }

        public static void NotifyItemDeleted(string path, bool isDirectory = false) {
            try {
                IntPtr pPath = Marshal.StringToHGlobalUni(path);
                string parent = Path.GetDirectoryName(path) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                uint eventId = isDirectory ? SHCNE_RMDIR : SHCNE_DELETE;
                SHChangeNotify(eventId, SHCNF_PATHW, pPath, IntPtr.Zero);
                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParent, IntPtr.Zero);
                }

                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }
            } catch {
                // Ignore notification failure
            }
        }

        public static BitmapSource? GetFileIconSource(string filePath, bool isLarge = true) {
            try {
                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | (isLarge ? SHGFI_LARGEICON : SHGFI_SMALLICON);

                if (!File.Exists(filePath) && !Directory.Exists(filePath)) {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                }

                IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (shinfo.hIcon != IntPtr.Zero) {
                    try {
                        using (Icon icon = Icon.FromHandle(shinfo.hIcon)) {
                            using Bitmap bitmap = icon.ToBitmap();
                            IntPtr hBitmap = bitmap.GetHbitmap();
                            try {
                                return Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                            } finally {
                                DeleteObject(hBitmap);
                            }
                        }
                    } finally {
                        DestroyIcon(shinfo.hIcon);
                    }
                }

                if (File.Exists(filePath)) {
                    using (Icon? assoc = Icon.ExtractAssociatedIcon(filePath)) {
                        if (assoc != null) {
                            using Bitmap bmp = assoc.ToBitmap();
                            IntPtr hBmp = bmp.GetHbitmap();
                            try {
                                return Imaging.CreateBitmapSourceFromHBitmap(
                                    hBmp,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                            } finally {
                                DeleteObject(hBmp);
                            }
                        }
                    }
                }
            } catch {
                // Fallback
            }
            return null;
        }

        public static Bitmap? GetFileIconBitmap(string filePath, bool isLarge = true) {
            try {
                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | (isLarge ? SHGFI_LARGEICON : SHGFI_SMALLICON);

                if (!File.Exists(filePath) && !Directory.Exists(filePath)) {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                }

                IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (shinfo.hIcon != IntPtr.Zero) {
                    try {
                        using (Icon icon = Icon.FromHandle(shinfo.hIcon)) {
                            return new Bitmap(icon.ToBitmap());
                        }
                    } finally {
                        DestroyIcon(shinfo.hIcon);
                    }
                }

                if (File.Exists(filePath)) {
                    using (Icon? assoc = Icon.ExtractAssociatedIcon(filePath)) {
                        if (assoc != null) {
                            return new Bitmap(assoc.ToBitmap());
                        }
                    }
                }
            } catch {
                // Fallback
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_BLURBEHIND {
            public uint dwFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fEnable;
            public IntPtr hRgnBlur;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fTransitionOnMaximized;
        }

        public const uint DWM_BB_ENABLE = 0x00000001;

        [DllImport("dwmapi.dll")]
        public static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

        public static Color GetWindowsAccentColor() {
            try {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (key != null) {
                    object? val = key.GetValue("ColorizationColor");
                    if (val is int intVal) {
                        uint colorVal = unchecked((uint)intVal);
                        byte r = (byte)((colorVal >> 16) & 0xFF);
                        byte g = (byte)((colorVal >> 8) & 0xFF);
                        byte b = (byte)(colorVal & 0xFF);
                        return Color.FromArgb(255, r, g, b);
                    }
                    val = key.GetValue("AccentColor");
                    if (val is int accentVal) {
                        uint colorVal = unchecked((uint)accentVal);
                        byte r = (byte)(colorVal & 0xFF);
                        byte g = (byte)((colorVal >> 8) & 0xFF);
                        byte b = (byte)((colorVal >> 16) & 0xFF);
                        return Color.FromArgb(255, r, g, b);
                    }
                }
            } catch {
                // Fallback
            }
            return Color.FromArgb(255, 0, 120, 212); // Windows Default Blue #0078D4
        }

        public const string DefaultPastelColorHex = "#FCDDC5";

        public static string GetWindowsAccentColorHex() {
            var c = GetWindowsAccentColor();
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public static Color ColorFromHsl(double hue, double s, double l) {
            hue = (hue % 360.0 + 360.0) % 360.0;
            s = Math.Clamp(s, 0.0, 1.0);
            l = Math.Clamp(l, 0.0, 1.0);

            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;

            double r = HueToRgb(p, q, hue / 360.0 + 1.0 / 3.0);
            double g = HueToRgb(p, q, hue / 360.0);
            double b = HueToRgb(p, q, hue / 360.0 - 1.0 / 3.0);

            return Color.FromArgb(
                255,
                (int)Math.Clamp(Math.Round(r * 255.0), 0, 255),
                (int)Math.Clamp(Math.Round(g * 255.0), 0, 255),
                (int)Math.Clamp(Math.Round(b * 255.0), 0, 255));
        }

        public static string ColorFromHslHex(double hue, double s, double l) {
            var c = ColorFromHsl(hue, s, l);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public static (string surfaceHex, string containerHex, string primaryHex) GetMaterialYouTonalPaletteHex(double hue) {
            double diff = Math.Abs(hue - 26.2);
            if (diff > 180) diff = 360 - diff;
            if (diff < 4.0) {
                return ("#FEF4EB", "#FCDDC5", "#7A5427");
            }

            string surface = ColorFromHslHex(hue + 2.2, 0.905, 0.959);
            string container = ColorFromHslHex(hue, 0.90, 0.88);
            string primary = ColorFromHslHex(hue + 6.3, 0.516, 0.316);
            return (surface, container, primary);
        }

        public static (Color surface, Color container, Color primary) GetMaterialYouTonalPalette(double hue) {
            double diff = Math.Abs(hue - 26.2);
            if (diff > 180) diff = 360 - diff;
            if (diff < 4.0) {
                return (
                    Color.FromArgb(255, 0xFE, 0xF4, 0xEB),
                    Color.FromArgb(255, 0xFC, 0xDD, 0xC5),
                    Color.FromArgb(255, 0x7A, 0x54, 0x27)
                );
            }

            var surface = ColorFromHsl(hue + 2.2, 0.905, 0.959);
            var container = ColorFromHsl(hue, 0.90, 0.88);
            var primary = ColorFromHsl(hue + 6.3, 0.516, 0.316);
            return (surface, container, primary);
        }

        public static (Color container, Color triangle, Color cloud, Color shield, Color ellipse) GetLogoDynamicColors(double hue, bool isDark) {
            hue = (hue % 360.0 + 360.0) % 360.0;
            if (isDark) {
                var container = ColorFromHsl(hue, 0.28, 0.24);
                var triangle = ColorFromHsl(hue, 0.75, 0.82);
                var cloud = ColorFromHsl(hue, 0.55, 0.68);
                var shield = ColorFromHsl((hue + 18.0) % 360.0, 0.65, 0.65);
                var ellipse = ColorFromHsl((hue + 349.0) % 360.0, 0.65, 0.65);
                return (container, triangle, cloud, shield, ellipse);
            } else {
                var container = ColorFromHsl(hue, 0.50, 0.80);
                var triangle = ColorFromHsl(hue, 0.75, 0.16);
                var cloud = ColorFromHsl(hue, 0.48, 0.36);
                var shield = ColorFromHsl((hue + 18.0) % 360.0, 0.61, 0.51);
                var ellipse = ColorFromHsl((hue + 349.0) % 360.0, 0.60, 0.51);
                return (container, triangle, cloud, shield, ellipse);
            }
        }

        public static Bitmap RenderUninstallerLogoBitmap(int size, double hue, bool isDark) {
            Bitmap? result = null;
            void CoreRender() {
                Color colContainer;
                Color colCloud;
                if (isDark) {
                    colContainer = ColorFromHsl(hue, 0.25, 0.20);
                    colCloud = ColorFromHsl(hue, 0.75, 0.78);
                } else {
                    var (cContainer, _, cCloud, _, _) = GetLogoDynamicColors(hue, false);
                    colContainer = cContainer;
                    colCloud = cCloud;
                }

                var dv = new System.Windows.Media.DrawingVisual();
                using (var dc = dv.RenderOpen()) {
                    double scale = size / 88.0;
                    dc.PushTransform(new System.Windows.Media.ScaleTransform(scale, scale));

                    System.Windows.Media.Color toWpf(Color c) => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                    var brushContainer = new System.Windows.Media.SolidColorBrush(toWpf(colContainer));
                    var brushCloud = new System.Windows.Media.SolidColorBrush(toWpf(colCloud));

                    // 1. Scalloped squircle container badge base
                    dc.DrawGeometry(brushContainer, null, System.Windows.Media.Geometry.Parse(
                        "M59.4086 1.20571C76.2803 -3.9745 92.0771 11.8223 86.8968 28.694L84.1639 37.595C82.8719 41.8029 82.8719 46.3016 84.1639 50.5095L86.8968 59.4105C92.0771 76.2822 76.2803 92.079 59.4086 86.8988L50.5076 84.1659C46.2997 82.8739 41.8009 82.8739 37.593 84.1659L28.6921 86.8988C11.8203 92.079 -3.97645 76.2822 1.20376 59.4105L3.93668 50.5095C5.22865 46.3016 5.22865 41.8029 3.93668 37.595L1.20376 28.694C-3.97645 11.8223 11.8203 -3.9745 28.692 1.20571L37.593 3.93863C41.8009 5.23061 46.2997 5.23061 50.5076 3.93863L59.4086 1.20571Z"));

                    // 2. Trash can container bin
                    dc.DrawGeometry(brushCloud, null, System.Windows.Media.Geometry.Parse(
                        "M35.6414 72C34.1731 72 32.9205 70.9372 32.6814 69.4885L26.5757 32.4885C26.2741 30.6613 27.6838 29 29.5356 29L59.5458 29C61.3698 29 62.7717 30.6142 62.5162 32.4202L57.2828 69.4202C57.0735 70.8999 55.8069 72 54.3123 72L35.6414 72Z"));

                    // 3. Trash can lid
                    dc.DrawRoundedRectangle(brushCloud, null, new System.Windows.Rect(26, 18, 37, 9), 3, 3);

                    dc.Pop();
                }

                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(dv);
                using var ms = new MemoryStream();
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                enc.Save(ms);
                ms.Position = 0;
                result = new Bitmap(ms);
            }

            if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA) {
                CoreRender();
            } else {
                var t = new System.Threading.Thread(CoreRender);
                t.SetApartmentState(System.Threading.ApartmentState.STA);
                t.Start();
                t.Join();
            }

            return result!;
        }

        public static Color ColorFromPastelHue(double hue) {
            return ColorFromHsl(hue, 0.90, 0.88);
        }

        private static double HueToRgb(double p, double q, double t) {
            if (t < 0) t += 1.0;
            if (t > 1) t -= 1.0;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }

        public static string ColorFromPastelHueHex(double hue) {
            var c = ColorFromPastelHue(hue);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public static double GetHueFromColor(Color color) {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            if (delta < 0.0001) return 26.2; // Default to #FCDDC5 peach hue if grayscale

            double hue;
            if (Math.Abs(max - r) < 0.0001) {
                hue = 60.0 * (((g - b) / delta) % 6);
            } else if (Math.Abs(max - g) < 0.0001) {
                hue = 60.0 * (((b - r) / delta) + 2);
            } else {
                hue = 60.0 * (((r - g) / delta) + 4);
            }

            if (hue < 0) hue += 360.0;
            return Math.Clamp(hue, 0.0, 360.0);
        }

        public static double GetHueFromHex(string hexColor) {
            try {
                if (string.IsNullOrWhiteSpace(hexColor)) {
                    return 26.2;
                }
                var c = ColorTranslator.FromHtml(hexColor);
                return GetHueFromColor(c);
            } catch {
                return 26.2;
            }
        }

        public static string GetWindowsAccentPastelHex() {
            try {
                var c = GetWindowsAccentColor();
                double r = c.R / 255.0;
                double g = c.G / 255.0;
                double b = c.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                if (max - min < 0.05) {
                    return DefaultPastelColorHex;
                }
                double hue = GetHueFromColor(c);
                return ColorFromPastelHueHex(hue);
            } catch {
                return DefaultPastelColorHex;
            }
        }

        public static string GetDefaultPastelColorHex() {
            return DefaultPastelColorHex;
        }

        public static BitmapSource? CaptureScreenArea(int x, int y, int width, int height, double dpiScale = 1.0) {
            try {
                if (width <= 0 || height <= 0) {
                    return null;
                }
                int physWidth = (int)Math.Max(1, Math.Round(width * dpiScale));
                int physHeight = (int)Math.Max(1, Math.Round(height * dpiScale));
                int physX = (int)Math.Round(x * dpiScale);
                int physY = (int)Math.Round(y * dpiScale);

                using var bmp = new System.Drawing.Bitmap(physWidth, physHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                    g.CopyFromScreen(physX, physY, 0, 0, new System.Drawing.Size(physWidth, physHeight), System.Drawing.CopyPixelOperation.SourceCopy);
                }

                IntPtr hBitmap = bmp.GetHbitmap();
                try {
                    var wpfBmp = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    wpfBmp.Freeze();
                    return wpfBmp;
                } finally {
                    DeleteObject(hBitmap);
                }
            } catch {
                return null;
            }
        }

        public static void EnableWindowBlur(IntPtr hwnd, uint tintColor = 0x66000000) {
            try {
                DWM_BLURBEHIND bb = new DWM_BLURBEHIND {
                    dwFlags = DWM_BB_ENABLE,
                    fEnable = true,
                    hRgnBlur = IntPtr.Zero,
                    fTransitionOnMaximized = false
                };
                DwmEnableBlurBehindWindow(hwnd, ref bb);

                AccentPolicy policy = new AccentPolicy {
                    AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                    AccentFlags = 2,
                    GradientColor = unchecked((int)tintColor),
                    AnimationId = 0
                };

                int size = Marshal.SizeOf(policy);
                IntPtr pPolicy = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(policy, pPolicy, false);

                WindowCompositionAttributeData data = new WindowCompositionAttributeData {
                    Attribute = 19, // WCA_ACCENT_POLICY
                    Data = pPolicy,
                    SizeOfData = size
                };

                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(pPolicy);
            } catch {
                // Fallback
            }
        }

        public static void EnableAcrylicBlur(IntPtr hwnd, uint accentColor = 0xCC1E1E1E) {
            try {
                int backdropType = 3; // DWMSBT_TRANSIENTWINDOW = 3 (Acrylic)
                DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
            } catch {
                // DWM acrylic attribute fallback
            }
        }

        public static bool IsWindowsDarkTheme() {
            try {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null) {
                    object? sysVal = key.GetValue("SystemUsesLightTheme");
                    if (sysVal is int intSys && intSys == 0) {
                        return true;
                    }
                    object? appVal = key.GetValue("AppsUseLightTheme");
                    if (appVal is int intApp && intApp == 0) {
                        return true;
                    }
                }
            } catch {
                // Fallback
            }
            return false;
        }
    }
}
