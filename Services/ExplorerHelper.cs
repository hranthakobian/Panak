#pragma warning disable CS8602
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;

namespace Panak.Services {
    public static class ExplorerHelper {
        public struct ItemInfo {
            public string? Path;
            public Rect BoundingBox;
            public bool IsFolder;
        }

        public static ItemInfo? GetItemUnderCursor() {
            try {
                if (!ShellHelper.GetCursorPos(out var pt)) {
                    return null;
                }

                Point winPoint = new Point(pt.X, pt.Y);
                AutomationElement? element = AutomationElement.FromPoint(winPoint);
                if (element == null) {
                    return null;
                }

                // Look for ListItem, TreeViewItem, or DataItem (WinUI File Explorer)
                AutomationElement? current = element;
                while (current != null && current != AutomationElement.RootElement) {
                    var controlType = current.Current.ControlType;
                    if (controlType == ControlType.ListItem || controlType == ControlType.TreeItem || controlType == ControlType.DataItem) {
                        break;
                    }
                    current = TreeWalker.ControlViewWalker.GetParent(current);
                }

                AutomationElement targetElement = current ?? element;
                Rect bounds = targetElement.Current.BoundingRectangle;

                string itemName = targetElement.Current.Name;
                if (string.IsNullOrWhiteSpace(itemName) && element != null) {
                    itemName = element.Current.Name;
                }
                if (string.IsNullOrWhiteSpace(itemName)) {
                    return null;
                }

                // Query Shell.Application for the file path
                string? path = GetPathFromShellWindows(pt, itemName);

                if (string.IsNullOrEmpty(path)) {
                    // Check desktop directories
                    string[] desktopDirs = new[] {
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop")
                    };

                    foreach (var d in desktopDirs) {
                        if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) {
                            string candidate = Path.Combine(d, itemName);
                            if (File.Exists(candidate) || Directory.Exists(candidate)) {
                                path = candidate;
                                break;
                            }
                            try {
                                var dirMatches = Directory.GetDirectories(d, itemName + ".*");
                                if (dirMatches.Length > 0) {
                                    path = dirMatches[0];
                                    break;
                                }
                                var matches = Directory.GetFiles(d, itemName + ".*");
                                if (matches.Length > 0) {
                                    path = matches[0];
                                    break;
                                }
                            } catch { }
                        }
                    }

                    // Also check known Panak folders
                    if (string.IsNullOrEmpty(path)) {
                        string? match = PanakManager.Instance.FindKnownFolderByName(itemName);
                        if (!string.IsNullOrEmpty(match)) {
                            path = match;
                        }
                    }
                }

                bool isFolder = false;
                if (!string.IsNullOrEmpty(path)) {
                    isFolder = Directory.Exists(path);
                }

                return new ItemInfo {
                    Path = path,
                    BoundingBox = bounds.IsEmpty ? new Rect(pt.X - 24, pt.Y - 24, 48, 48) : bounds,
                    IsFolder = isFolder
                };
            } catch {
                return null;
            }
        }

        public static Rect GetBoundingBoxForPath(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) {
                return Rect.Empty;
            }

            string folderName = Path.GetFileName(folderPath.TrimEnd('\\', '/'));

            // 1. Check active Explorer / Desktop selected item
            try {
                var selectedInfo = GetSelectedItemInActiveExplorer();
                if (selectedInfo != null && !selectedInfo.Value.BoundingBox.IsEmpty) {
                    if (string.Equals(selectedInfo.Value.Path, folderPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(selectedInfo.Value.Path), folderName, StringComparison.OrdinalIgnoreCase)) {
                        return selectedInfo.Value.BoundingBox;
                    }
                }
            } catch {
                // Ignore
            }

            // 2. Search UIAutomation elements in Foreground window first
            try {
                IntPtr fgHwnd = ShellHelper.GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero) {
                    AutomationElement? fgElem = AutomationElement.FromHandle(fgHwnd);
                    if (fgElem != null) {
                        var found = fgElem.FindFirst(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.NameProperty, folderName));
                        if (found != null) {
                            Rect r = found.Current.BoundingRectangle;
                            if (!r.IsEmpty && r.Width > 0 && r.Height > 0) {
                                return r;
                            }
                        }
                    }
                }
            } catch {
                // Ignore
            }

            // 3. Search Desktop list in UIAutomation
            try {
                AutomationElement? root = AutomationElement.RootElement;
                if (root != null) {
                    var found = root.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.NameProperty, folderName));
                    if (found != null) {
                        Rect r = found.Current.BoundingRectangle;
                        if (!r.IsEmpty && r.Width > 0 && r.Height > 0) {
                            return r;
                        }
                    }
                }
            } catch {
                // Ignore
            }

            // 4. Fallback to cursor position
            if (ShellHelper.GetCursorPos(out var pt)) {
                return new Rect(pt.X - 24, pt.Y - 24, 48, 48);
            }

            return Rect.Empty;
        }

        public static ItemInfo? GetSelectedItemInActiveExplorer() {
            try {
                IntPtr activeHwnd = ShellHelper.GetForegroundWindow();
                if (activeHwnd == IntPtr.Zero) {
                    return null;
                }

                StringBuilder className = new StringBuilder(256);
                ShellHelper.GetClassName(activeHwnd, className, className.Capacity);
                string cls = className.ToString();

                bool isExplorer = cls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                                  cls.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                                  cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);

                if (!isExplorer) {
                    return null;
                }

                // Get focused element from UIAutomation
                AutomationElement? focused = AutomationElement.FocusedElement;
                Rect bounds = Rect.Empty;
                if (focused != null) {
                    bounds = focused.Current.BoundingRectangle;
                }

                // Query Shell.Application for active window
                string? selectedPath = GetActiveExplorerSelectedPath(activeHwnd);

                if (string.IsNullOrEmpty(selectedPath) && focused != null && !string.IsNullOrWhiteSpace(focused.Current.Name)) {
                    string focusedName = focused.Current.Name;
                    string[] desktopDirs = new[] {
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop")
                    };

                    foreach (var d in desktopDirs) {
                        if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) {
                            string candidate = Path.Combine(d, focusedName);
                            if (File.Exists(candidate) || Directory.Exists(candidate)) {
                                selectedPath = candidate;
                                break;
                            }
                            try {
                                var dirMatches = Directory.GetDirectories(d, focusedName + ".*");
                                if (dirMatches.Length > 0) {
                                    selectedPath = dirMatches[0];
                                    break;
                                }
                                var matches = Directory.GetFiles(d, focusedName + ".*");
                                if (matches.Length > 0) {
                                    selectedPath = matches[0];
                                    break;
                                }
                            } catch { }
                        }
                    }
                }

                if (string.IsNullOrEmpty(selectedPath)) {
                    return null;
                }

                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) {
                    if (ShellHelper.GetCursorPos(out var pt)) {
                        bounds = new Rect(pt.X - 24, pt.Y - 24, 48, 48);
                    } else {
                        bounds = new Rect(100, 100, 48, 48);
                    }
                }

                return new ItemInfo {
                    Path = selectedPath,
                    BoundingBox = bounds,
                    IsFolder = Directory.Exists(selectedPath)
                };
            } catch {
                return null;
            }
        }

        private static string? GetPathFromShellWindows(ShellHelper.POINT pt, string itemName) {
            try {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) {
                    return null;
                }

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) {
                    return null;
                }

                dynamic windows = shell.Windows();
                int count = windows.Count;

                for (int i = 0; i < count; i++) {
                    dynamic item = windows.Item(i);
                    if (item == null) {
                        continue;
                    }

                    try {
                        dynamic doc = item.Document;
                        if (doc == null) {
                            continue;
                        }

                        dynamic? selected = doc.SelectedItems();
                        if (selected != null && (int)selected.Count > 0) {
                            int cnt = (int)selected.Count;
                            for (int s = 0; s < cnt; s++) {
                                dynamic? sel = selected.Item(s);
                                if (sel != null) {
                                    string? path = sel.Path;
                                    if (!string.IsNullOrEmpty(path)) {
                                        if (!string.IsNullOrWhiteSpace(itemName) && Path.GetFileName(path).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                            return path;
                                        }
                                    }
                                }
                            }
                        }

                        // Also check focused item
                        dynamic? focusedItem = doc.FocusedItem;
                        if (focusedItem != null) {
                            string? fpath = focusedItem.Path;
                            if (!string.IsNullOrEmpty(fpath)) {
                                if (!string.IsNullOrWhiteSpace(itemName) && Path.GetFileName(fpath).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                    return fpath;
                                }
                            }
                        }

                        // Also check folder path + itemName
                        try {
                            dynamic? folderObj = doc.Folder;
                            if (folderObj != null) {
                                dynamic? selfObj = folderObj.Self;
                                if (selfObj != null) {
                                    string? folderPath = (string?)selfObj.Path;
                                    if (!string.IsNullOrEmpty(folderPath) && !string.IsNullOrWhiteSpace(itemName)) {
                                        string direct = Path.Combine(folderPath, itemName);
                                        if (File.Exists(direct) || Directory.Exists(direct)) {
                                            return direct;
                                        }
                                        try {
                                            var dirMatches = Directory.GetDirectories(folderPath, itemName + ".*");
                                            if (dirMatches.Length > 0) {
                                                return dirMatches[0];
                                            }
                                            var matches = Directory.GetFiles(folderPath, itemName + ".*");
                                            if (matches.Length > 0) {
                                                return matches[0];
                                            }
                                        } catch { }
                                    }
                                }
                            }
                        } catch {
                            // Fallback
                        }
                    } catch {
                        // Skip tab or window if Document property not available
                    }
                }
            } catch {
                // COM error
            }
            return null;
        }

        private static string? GetActiveExplorerSelectedPath(IntPtr activeHwnd) {
            try {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) {
                    return null;
                }

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) {
                    return null;
                }

                dynamic windows = shell.Windows();
                int count = windows.Count;
                IntPtr rootHwnd = ShellHelper.GetAncestor(activeHwnd, ShellHelper.GA_ROOT);

                for (int i = 0; i < count; i++) {
                    dynamic item = windows.Item(i);
                    if (item == null) {
                        continue;
                    }

                    try {
                        long hwnd = item.HWND;
                        if (hwnd == activeHwnd.ToInt64() || (rootHwnd != IntPtr.Zero && hwnd == rootHwnd.ToInt64())) {
                            dynamic? doc = item.Document;
                            if (doc != null) {
                                dynamic? focusedItem = doc.FocusedItem;
                                if (focusedItem != null) {
                                    return (string?)focusedItem.Path;
                                }

                                dynamic? selected = doc.SelectedItems();
                                if (selected != null && (int)selected.Count > 0) {
                                    dynamic? first = selected.Item(0);
                                    if (first != null) {
                                        return (string?)first.Path;
                                    }
                                }
                            }
                        }
                    } catch {
                        // Skip window
                    }
                }
            } catch {
                // COM error
            }
            return null;
        }

        public static List<string> GetActiveExplorerSelectedPaths() {
            var result = new List<string>();
            try {
                IntPtr activeHwnd = ShellHelper.GetForegroundWindow();
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) {
                    return result;
                }
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) {
                    return result;
                }

                dynamic windows = shell.Windows();
                int count = windows.Count;

                for (int i = 0; i < count; i++) {
                    dynamic item = windows.Item(i);
                    if (item == null) {
                        continue;
                    }

                    try {
                        long hwnd = item.HWND;
                        if (activeHwnd == IntPtr.Zero || hwnd == activeHwnd.ToInt64()) {
                            dynamic? doc = item.Document;
                            if (doc != null) {
                                dynamic? selected = doc.SelectedItems();
                                if (selected != null && (int)selected.Count > 0) {
                                    int cnt = (int)selected.Count;
                                    for (int s = 0; s < cnt; s++) {
                                        dynamic? sel = selected.Item(s);
                                        if (sel != null) {
                                            string? path = (string?)sel.Path;
                                            if (!string.IsNullOrEmpty(path)) {
                                                result.Add(path);
                                            }
                                        }
                                    }
                                    if (result.Count > 0) {
                                        return result;
                                    }
                                }

                                dynamic? focusedItem = doc.FocusedItem;
                                if (focusedItem != null) {
                                    string? fpath = (string?)focusedItem.Path;
                                    if (!string.IsNullOrEmpty(fpath)) {
                                        result.Add(fpath);
                                        return result;
                                    }
                                }
                            }
                        }
                    } catch {
                        // Skip
                    }
                }
            } catch {
                // COM error
            }

            // Fallback: check selected elements in UIAutomation
            try {
                IntPtr fg = ShellHelper.GetForegroundWindow();
                if (fg != IntPtr.Zero) {
                    AutomationElement? root = AutomationElement.FromHandle(fg);
                    if (root != null) {
                        string[] desktopDirs = new[] {
                            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop")
                        };
                        var selectedCondition = new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true);
                        var selectedElements = root.FindAll(TreeScope.Descendants, selectedCondition);
                        if (selectedElements != null && selectedElements.Count > 0) {
                            foreach (AutomationElement el in selectedElements) {
                                string name = el.Current.Name;
                                if (string.IsNullOrEmpty(name)) {
                                    continue;
                                }
                                foreach (var d in desktopDirs) {
                                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) {
                                        string candidate = Path.Combine(d, name);
                                        if (File.Exists(candidate) || Directory.Exists(candidate)) {
                                            if (!result.Contains(candidate, StringComparer.OrdinalIgnoreCase)) {
                                                result.Add(candidate);
                                            }
                                            break;
                                        }
                                        try {
                                            var matches = Directory.GetFiles(d, name + ".*");
                                            if (matches.Length > 0 && !result.Contains(matches[0], StringComparer.OrdinalIgnoreCase)) {
                                                result.Add(matches[0]);
                                                break;
                                            }
                                        } catch { }
                                    }
                                }
                            }
                        }
                    }
                }
            } catch {
                // Fallback
            }

            return result;
        }

        public static bool IsFocusInsideTextEdit() {
            try {
                AutomationElement? focused = AutomationElement.FocusedElement;
                if (focused != null) {
                    var ct = focused.Current.ControlType;
                    if (ct == ControlType.Edit || ct == ControlType.Document) {
                        return true;
                    }
                }
            } catch {
                // Fallback
            }
            return false;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr GetDesktopListView() {
            // 1. Try Progman
            IntPtr progman = ShellHelper.FindWindow("Progman", null);
            if (progman != IntPtr.Zero) {
                IntPtr defView = ShellHelper.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero) {
                    IntPtr lv = ShellHelper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                    if (lv != IntPtr.Zero) {
                        return lv;
                    }
                }
            }

            // 2. Try WorkerW windows (when desktop wallpaper/active desktop is running)
            IntPtr worker = IntPtr.Zero;
            do {
                worker = ShellHelper.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
                if (worker != IntPtr.Zero) {
                    IntPtr defView = ShellHelper.FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero) {
                        IntPtr lv = ShellHelper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                        if (lv != IntPtr.Zero) {
                            return lv;
                        }
                    }
                }
            } while (worker != IntPtr.Zero);

            // 3. Try GetShellWindow
            IntPtr shellWnd = ShellHelper.GetShellWindow();
            if (shellWnd != IntPtr.Zero) {
                IntPtr defView = ShellHelper.FindWindowEx(shellWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero) {
                    IntPtr lv = ShellHelper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                    if (lv != IntPtr.Zero) {
                        return lv;
                    }
                }
            }

            return IntPtr.Zero;
        }

        public static void SetDesktopItemPosition(string itemName, int x, int y) {
            System.Threading.Tasks.Task.Run(async () => {
                for (int attempt = 0; attempt < 6; attempt++) {
                    await System.Threading.Tasks.Task.Delay(attempt == 0 ? 80 : 160);

                    try {
                        IntPtr listview = GetDesktopListView();
                        if (listview != IntPtr.Zero) {
                            AutomationElement? desktopElem = AutomationElement.FromHandle(listview);
                            if (desktopElem != null) {
                                var children = desktopElem.FindAll(TreeScope.Children,
                                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                                if (children != null && children.Count > 0) {
                                    int itemIndex = -1;
                                    for (int i = 0; i < children.Count; i++) {
                                        string name = children[i].Current.Name;
                                        if (name.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                                            name.StartsWith(itemName, StringComparison.OrdinalIgnoreCase)) {
                                            itemIndex = i;
                                            break;
                                        }
                                    }

                                    if (itemIndex >= 0) {
                                        int posX = Math.Max(10, x - 24);
                                        int posY = Math.Max(10, y - 24);

                                        IntPtr lparam = (IntPtr)((short)posY << 16 | (ushort)(short)posX);
                                        SendMessage(listview, 0x100F /* LVM_SETITEMPOSITION */, (IntPtr)itemIndex, lparam);
                                        return;
                                    }
                                }
                            }
                        }
                    } catch {
                        // Ignore
                    }
                }
            });
        }
    }
}
