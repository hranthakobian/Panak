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
                ShellHelper.POINT pt = default;
                bool hasCursor = ShellHelper.GetCursorPos(out pt);

                string cls = string.Empty;
                if (activeHwnd != IntPtr.Zero) {
                    StringBuilder className = new StringBuilder(256);
                    ShellHelper.GetClassName(activeHwnd, className, className.Capacity);
                    cls = className.ToString();
                }

                bool isExplorer = cls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                                  cls.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                                  cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);

                IntPtr targetHwnd = activeHwnd;

                // If foreground window is not Explorer/Desktop, check window under cursor
                if (!isExplorer && hasCursor) {
                    IntPtr hwndUnderPoint = ShellHelper.WindowFromPoint(pt);
                    IntPtr rootUnderPoint = ShellHelper.GetAncestor(hwndUnderPoint, ShellHelper.GA_ROOT);
                    if (rootUnderPoint != IntPtr.Zero) {
                        StringBuilder ptCls = new StringBuilder(256);
                        ShellHelper.GetClassName(rootUnderPoint, ptCls, ptCls.Capacity);
                        string ptClsStr = ptCls.ToString();
                        if (ptClsStr.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                            ptClsStr.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                            ptClsStr.Equals("WorkerW", StringComparison.OrdinalIgnoreCase)) {
                            targetHwnd = rootUnderPoint;
                            cls = ptClsStr;
                            isExplorer = true;
                        }
                    }
                }

                if (!isExplorer) {
                    return null;
                }

                bool isDesktop = cls.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                                 cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);

                // FIRST: Query Shell COM API (< 1ms), avoids blocking or hook timeout on Windows 10
                string? selectedPath = isDesktop ? GetDesktopSelectedPathFast() : GetActiveExplorerSelectedPath(targetHwnd);

                // If not found yet and target was not detected as desktop, desktop might still have focus
                if (string.IsNullOrEmpty(selectedPath) && !isDesktop) {
                    selectedPath = GetDesktopSelectedPathFast();
                }

                Rect bounds = hasCursor ? new Rect(pt.X - 24, pt.Y - 24, 48, 48) : Rect.Empty;

                // Fallback to UIAutomation ONLY if COM did not return path
                if (string.IsNullOrEmpty(selectedPath)) {
                    AutomationElement? focused = null;
                    try {
                        focused = AutomationElement.FocusedElement;
                        if (focused != null) {
                            bounds = focused.Current.BoundingRectangle;
                        }
                    } catch { }

                    if (focused != null && !string.IsNullOrWhiteSpace(focused.Current.Name)) {
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
                }

                if (string.IsNullOrEmpty(selectedPath)) {
                    return null;
                }

                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) {
                    if (hasCursor) {
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

        public static string? GetDesktopSelectedPathFast() {
            try {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return null;
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return null;
                dynamic windows = shell.Windows();
                IShellWindows sw = (IShellWindows)windows;
                object loc = 0; // CSIDL_DESKTOP
                object locRoot = 0;
                int hwnd = 0;
                // SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1
                object disp = sw.FindWindowSW(ref loc, ref locRoot, 8, out hwnd, 1);
                if (disp != null) {
                    dynamic d = disp;
                    dynamic? doc = d.Document;
                    if (doc != null) {
                        dynamic? focusedItem = doc.FocusedItem;
                        if (focusedItem != null) {
                            string? fpath = (string?)focusedItem.Path;
                            if (!string.IsNullOrEmpty(fpath)) return fpath;
                        }

                        dynamic? selected = doc.SelectedItems();
                        if (selected != null && (int)selected.Count > 0) {
                            dynamic? first = selected.Item(0);
                            if (first != null) {
                                string? spath = (string?)first.Path;
                                if (!string.IsNullOrEmpty(spath)) return spath;
                            }
                        }
                    }
                }
            } catch { }
            return null;
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

            try {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null) {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null) {
                        dynamic windows = shell.Windows();
                        IShellWindows sw = (IShellWindows)windows;
                        object loc = 0; // CSIDL_DESKTOP
                        object locRoot = 0;
                        int hwnd = 0;
                        object disp = sw.FindWindowSW(ref loc, ref locRoot, 8, out hwnd, 1);
                        if (disp != null) {
                            dynamic d = disp;
                            dynamic? doc = d.Document;
                            if (doc != null) {
                                dynamic? focusedItem = doc.FocusedItem;
                                if (focusedItem != null) {
                                    string? fpath = focusedItem.Path;
                                    if (!string.IsNullOrEmpty(fpath) && !string.IsNullOrWhiteSpace(itemName) &&
                                        Path.GetFileName(fpath).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                        return fpath;
                                    }
                                }
                                dynamic? selected = doc.SelectedItems();
                                if (selected != null && (int)selected.Count > 0) {
                                    int cnt = (int)selected.Count;
                                    for (int s = 0; s < cnt; s++) {
                                        dynamic? sel = selected.Item(s);
                                        if (sel != null) {
                                            string? path = sel.Path;
                                            if (!string.IsNullOrEmpty(path) && !string.IsNullOrWhiteSpace(itemName) &&
                                                Path.GetFileName(path).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                                return path;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } catch { }

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

        [ComImport]
        [Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IShellWindows {
            int Count { get; }
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object Item([In, Optional] object index);
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object FindWindowSW([In] ref object pvarLoc, [In] ref object pvarLocRoot, [In] int swClass, out int pHWND, [In] int swFlags);
        }

        public static List<string> GetDesktopSelectedPaths() {
            var result = new List<string>();
            try {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return result;
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return result;
                dynamic windows = shell.Windows();
                IShellWindows sw = (IShellWindows)windows;
                object loc = 0; // CSIDL_DESKTOP
                object locRoot = 0;
                int hwnd = 0;
                // SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1
                object disp = sw.FindWindowSW(ref loc, ref locRoot, 8, out hwnd, 1);
                if (disp != null) {
                    dynamic d = disp;
                    dynamic? doc = d.Document;
                    if (doc != null) {
                        dynamic? selected = doc.SelectedItems();
                        if (selected != null && (int)selected.Count > 0) {
                            int cnt = (int)selected.Count;
                            for (int s = 0; s < cnt; s++) {
                                dynamic? sel = selected.Item(s);
                                if (sel != null) {
                                    string? path = (string?)sel.Path;
                                    if (!string.IsNullOrEmpty(path) && !result.Contains(path, StringComparer.OrdinalIgnoreCase)) {
                                        result.Add(path);
                                    }
                                }
                            }
                        }
                    }
                }
            } catch { }
            return result;
        }

        public static List<string> GetSelectedPathsForTarget(string? targetPath = null) {
            var result = new List<string>();
            bool isTargetOnDesktop = false;
            string targetDir = string.Empty;

            if (!string.IsNullOrEmpty(targetPath)) {
                targetDir = Path.GetDirectoryName(targetPath) ?? string.Empty;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string oneDriveDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop");

                if (string.Equals(targetDir, desktop, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(targetDir, commonDesktop, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(targetDir, oneDriveDesktop, StringComparison.OrdinalIgnoreCase)) {
                    isTargetOnDesktop = true;
                }
            }

            // 1. If target is on Desktop or target not specified, check Desktop selection first
            if (isTargetOnDesktop || string.IsNullOrEmpty(targetPath)) {
                var desktopItems = GetDesktopSelectedPaths();
                if (desktopItems.Count > 0) {
                    if (string.IsNullOrEmpty(targetPath) || desktopItems.Contains(targetPath, StringComparer.OrdinalIgnoreCase)) {
                        return desktopItems;
                    }
                }
            }

            // 2. Query open Explorer windows via Shell.Application
            try {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null) {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null) {
                        dynamic windows = shell.Windows();
                        int count = windows.Count;

                        // First pass: look for window whose SelectedItems contains targetPath
                        for (int i = 0; i < count; i++) {
                            dynamic item = windows.Item(i);
                            if (item == null) continue;
                            try {
                                dynamic? doc = item.Document;
                                if (doc != null) {
                                    dynamic? selected = doc.SelectedItems();
                                    if (selected != null && (int)selected.Count > 0) {
                                        var windowList = new List<string>();
                                        int cnt = (int)selected.Count;
                                        for (int s = 0; s < cnt; s++) {
                                            dynamic? sel = selected.Item(s);
                                            if (sel != null) {
                                                string? p = (string?)sel.Path;
                                                if (!string.IsNullOrEmpty(p) && !windowList.Contains(p, StringComparer.OrdinalIgnoreCase)) {
                                                    windowList.Add(p);
                                                }
                                            }
                                        }

                                        if (!string.IsNullOrEmpty(targetPath) && windowList.Contains(targetPath, StringComparer.OrdinalIgnoreCase)) {
                                            return windowList;
                                        }

                                        if (string.IsNullOrEmpty(targetPath) && windowList.Count > 0) {
                                            IntPtr activeHwnd = ShellHelper.GetForegroundWindow();
                                            IntPtr rootHwnd = ShellHelper.GetAncestor(activeHwnd, ShellHelper.GA_ROOT);
                                            long hwnd = item.HWND;
                                            if (hwnd == activeHwnd.ToInt64() || (rootHwnd != IntPtr.Zero && hwnd == rootHwnd.ToInt64())) {
                                                return windowList;
                                            }
                                            if (result.Count == 0) {
                                                result = windowList;
                                            }
                                        }
                                    }
                                }
                            } catch { }
                        }

                        // Second pass: match by window's folder path
                        if (!string.IsNullOrEmpty(targetDir)) {
                            for (int i = 0; i < count; i++) {
                                dynamic item = windows.Item(i);
                                if (item == null) continue;
                                try {
                                    dynamic? doc = item.Document;
                                    if (doc != null) {
                                        string? folderPath = (string?)doc.Folder?.Self?.Path;
                                        if (!string.IsNullOrEmpty(folderPath) && folderPath.TrimEnd('\\').Equals(targetDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) {
                                            dynamic? selected = doc.SelectedItems();
                                            if (selected != null && (int)selected.Count > 0) {
                                                var windowList = new List<string>();
                                                int cnt = (int)selected.Count;
                                                for (int s = 0; s < cnt; s++) {
                                                    dynamic? sel = selected.Item(s);
                                                    if (sel != null) {
                                                        string? p = (string?)sel.Path;
                                                        if (!string.IsNullOrEmpty(p) && !windowList.Contains(p, StringComparer.OrdinalIgnoreCase)) {
                                                            windowList.Add(p);
                                                        }
                                                    }
                                                }
                                                if (windowList.Count > 0) {
                                                    return windowList;
                                                }
                                            }
                                        }
                                    }
                                } catch { }
                            }
                        }
                    }
                }
            } catch { }

            // 3. Check Desktop listview via UIAutomation directly
            try {
                IntPtr listview = GetDesktopListView();
                if (listview != IntPtr.Zero) {
                    AutomationElement? desktopElem = AutomationElement.FromHandle(listview);
                    if (desktopElem != null) {
                        var selectedCondition = new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true);
                        var selectedElements = desktopElem.FindAll(TreeScope.Children, selectedCondition);
                        if (selectedElements != null && selectedElements.Count > 0) {
                            string[] desktopDirs = new[] {
                                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop")
                            };

                            var desktopSelected = new List<string>();
                            foreach (AutomationElement el in selectedElements) {
                                string name = el.Current.Name;
                                if (string.IsNullOrEmpty(name)) continue;
                                foreach (var d in desktopDirs) {
                                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) {
                                        string candidate = Path.Combine(d, name);
                                        if (File.Exists(candidate) || Directory.Exists(candidate)) {
                                            if (!desktopSelected.Contains(candidate, StringComparer.OrdinalIgnoreCase)) {
                                                desktopSelected.Add(candidate);
                                            }
                                            break;
                                        }
                                    }
                                }
                            }

                            if (desktopSelected.Count > 0) {
                                if (string.IsNullOrEmpty(targetPath) || desktopSelected.Contains(targetPath, StringComparer.OrdinalIgnoreCase)) {
                                    return desktopSelected;
                                }
                            }
                        }
                    }
                }
            } catch { }

            // 4. Return any items found in open windows
            if (result.Count > 0) {
                return result;
            }

            // 5. If specific target was given and not found in any multi-selection, return the single target
            if (!string.IsNullOrEmpty(targetPath)) {
                return new List<string> { targetPath };
            }

            return result;
        }

        public static List<string> GetActiveExplorerSelectedPaths() {
            return GetSelectedPathsForTarget(null);
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
