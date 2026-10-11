using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Panak.Models;
using Panak.Views;

namespace Panak.Services {
    public class PanakManager {
        private static PanakManager? _instance;
        public static PanakManager Instance => _instance ??= new PanakManager();

        private FolderPopupWindow? _popupWindow;
        private BackdropWindow? _backdropWindow;
        private bool _isOpen = false;
        private string _currentOpenPath = string.Empty;

        private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher? _currentParentWatcher;
        private readonly HashSet<string> _knownFolders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, System.Threading.Timer> _debounceTimers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _lastUpdateTicks = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _debounceLock = new();

        public bool IsPopupOpen => _isOpen;
        public bool IsPopupWithinGracePeriod => _popupWindow != null && _popupWindow.IsWithinOpenGracePeriod();

        public IntPtr GetPopupHwnd() {
            if (_popupWindow != null && _popupWindow.IsVisible) {
                return new System.Windows.Interop.WindowInteropHelper(_popupWindow).Handle;
            }
            return IntPtr.Zero;
        }

        public void UpdateOpenPopupThemeColor(string hexColor) {
            if (_isOpen && _popupWindow != null && _popupWindow.IsVisible) {
                _popupWindow.Dispatcher.Invoke(() => {
                    _popupWindow.ApplyThemeColor(hexColor);
                });
            }
        }

        public void UpdateOpenPopupThemeMode(string mode) {
            if (_isOpen && _popupWindow != null && _popupWindow.IsVisible) {
                _popupWindow.Dispatcher.Invoke(() => {
                    _popupWindow.ApplyThemeMode(mode);
                });
            }
        }

        public PanakManager() {
            ScanDesktopForPanakFolders();
        }

        public void ScanDesktopForPanakFolders() {
            try {
                string[] scanDirs = new[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop")
                };

                foreach (var dir in scanDirs) {
                    if (Directory.Exists(dir)) {
                        foreach (var sub in Directory.GetDirectories(dir)) {
                            if (CheckIfPanakFolder(sub)) {
                                _knownFolders.Add(sub);
                                EnsurePanakFolderIntegrity(sub);
                                WatchFolder(sub);
                            }
                        }

                        WatchDesktopDirectory(dir);
                    }
                }
            } catch {
                // Ignore initial scan errors
            }
        }

        private readonly List<FileSystemWatcher> _desktopWatchers = new();

        private void WatchDesktopDirectory(string dir) {
            try {
                var watcher = new FileSystemWatcher(dir) {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                void OnDesktopSubdirEvent(object sender, FileSystemEventArgs e) {
                    void CheckAndRegister() {
                        if (Directory.Exists(e.FullPath) && CheckIfPanakFolder(e.FullPath)) {
                            _knownFolders.Add(e.FullPath);
                            EnsurePanakFolderIntegrity(e.FullPath);
                            WatchFolder(e.FullPath);
                        }
                    }
                    CheckAndRegister();
                    Task.Delay(350).ContinueWith(_ => CheckAndRegister());
                }

                watcher.Created += OnDesktopSubdirEvent;
                watcher.Renamed += (s, e) => {
                    _knownFolders.Remove(e.OldFullPath);
                    if (string.Equals(e.OldFullPath, _currentOpenPath, StringComparison.OrdinalIgnoreCase)) {
                        RenameFolder(e.OldFullPath, e.FullPath);
                        Application.Current?.Dispatcher.BeginInvoke(() => {
                            if (_isOpen && _popupWindow != null) {
                                _popupWindow.OnExternalFolderRenamed(e.OldFullPath, e.FullPath);
                            }
                        });
                    }
                    OnDesktopSubdirEvent(s, e);
                };

                _desktopWatchers.Add(watcher);
            } catch { }
        }

        public void EnsurePanakFolderIntegrity(string folderPath) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                var folderAttrs = File.GetAttributes(folderPath);
                if (!folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                    File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);
                }

                string desktopIni = Path.Combine(folderPath, "desktop.ini");
                bool needsRegen = false;

                if (!File.Exists(desktopIni)) {
                    needsRegen = true;
                } else {
                    var iniAttrs = File.GetAttributes(desktopIni);
                    if (!iniAttrs.HasFlag(FileAttributes.System) || !iniAttrs.HasFlag(FileAttributes.Hidden)) {
                        File.SetAttributes(desktopIni, iniAttrs | FileAttributes.System | FileAttributes.Hidden);
                    }
                    string[] icos = Directory.GetFiles(folderPath, "panak_icon*.ico");
                    if (icos.Length == 0) {
                        needsRegen = true;
                    } else {
                        foreach (var ico in icos) {
                            var icoAttrs = File.GetAttributes(ico);
                            if (!icoAttrs.HasFlag(FileAttributes.System) || !icoAttrs.HasFlag(FileAttributes.Hidden)) {
                                File.SetAttributes(ico, icoAttrs | FileAttributes.System | FileAttributes.Hidden);
                            }
                        }
                    }
                }

                if (needsRegen) {
                    var folderData = PanakFolderData.Load(folderPath);
                    IconGenerator.GenerateFolderIcon(folderPath, folderData.IconShape, folderData.Color, folderData.BackgroundOpacity);
                }
            } catch {
                // Ignore integrity check errors
            }
        }

        public bool IsPanakFolder(string? path) {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
                return false;
            }

            return CheckIfPanakFolder(path);
        }

        public static bool CheckIfPanakFolder(string path) {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
                return false;
            }

            if (path.EndsWith(".panak", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string metaFile = Path.Combine(path, "panak.json");
            if (File.Exists(metaFile)) {
                try {
                    var data = PanakFolderData.Load(path);
                    return data.AutoPopup;
                } catch {
                    return true;
                }
            }

            return false;
        }

        public string? FindKnownFolderByName(string? folderName) {
            if (string.IsNullOrEmpty(folderName)) {
                return null;
            }

            foreach (var folder in _knownFolders) {
                if (Path.GetFileName(folder).Equals(folderName, StringComparison.OrdinalIgnoreCase)) {
                    return folder;
                }
            }

            return null;
        }

        public static List<FileSystemInfo> GetUserItems(string folderPath) {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                return new List<FileSystemInfo>();
            }
            try {
                var directoryInfo = new DirectoryInfo(folderPath);
                return directoryInfo.GetFileSystemInfos()
                    .Where(f => !f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.Equals("panak.json", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                                !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .OrderBy(f => !f.Attributes.HasFlag(FileAttributes.Directory))
                    .ThenBy(f => f.Name)
                    .ToList();
            } catch {
                return new List<FileSystemInfo>();
            }
        }

        public bool CheckAndDissolveFolder(string folderPath) {
            try {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                    return false;
                }

                if (!CheckIfPanakFolder(folderPath)) {
                    return false;
                }

                var userItems = GetUserItems(folderPath);
                if (userItems.Count > 1) {
                    return false;
                }

                // If 1 item remains inside, move it out to parent directory before deleting folder
                string? parentDir = Path.GetDirectoryName(folderPath);
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) {
                    parentDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                if (userItems.Count == 1) {
                    var remaining = userItems[0];
                    string targetName = remaining.Name;
                    string targetPath = Path.Combine(parentDir, targetName);

                    int counter = 1;
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(targetName);
                    string ext = Path.GetExtension(targetName);
                    bool isDir = remaining.Attributes.HasFlag(FileAttributes.Directory);

                    while (File.Exists(targetPath) || Directory.Exists(targetPath)) {
                        string uniqueName = isDir ? $"{targetName} ({counter++})" : $"{nameWithoutExt} ({counter++}){ext}";
                        targetPath = Path.Combine(parentDir, uniqueName);
                    }

                    try {
                        if (isDir) {
                            Directory.Move(remaining.FullName, targetPath);
                        } else {
                            File.Move(remaining.FullName, targetPath);
                        }

                        try {
                            var attrs = File.GetAttributes(targetPath);
                            if (attrs.HasFlag(FileAttributes.ReadOnly)) {
                                File.SetAttributes(targetPath, attrs & ~FileAttributes.ReadOnly);
                            }
                        } catch { }

                        ShellHelper.NotifyShellItemChanged(targetPath);
                        ShellHelper.NotifyFolderCreated(targetPath);
                    } catch {
                        // Abort deletion to avoid user data loss if move fails
                        return false;
                    }
                }

                // Close popup window if currently open for this folder
                Application.Current?.Dispatcher.Invoke(() => {
                    if (_isOpen && string.Equals(_currentOpenPath, folderPath, StringComparison.OrdinalIgnoreCase)) {
                        ClosePopup();
                    }
                });

                // Unregister and stop watching
                _knownFolders.Remove(folderPath);
                lock (_watchers) {
                    if (_watchers.TryGetValue(folderPath, out var w)) {
                        try {
                            w.EnableRaisingEvents = false;
                            w.Dispose();
                        } catch { }
                        _watchers.Remove(folderPath);
                    }
                }

                lock (_debounceLock) {
                    if (_debounceTimers.TryGetValue(folderPath, out var timer)) {
                        try { timer.Dispose(); } catch { }
                        _debounceTimers.Remove(folderPath);
                    }
                    _lastUpdateTicks.Remove(folderPath);
                }

                // Clean up system files and remove ReadOnly on folder
                try {
                    string desktopIni = Path.Combine(folderPath, "desktop.ini");
                    if (File.Exists(desktopIni)) {
                        File.SetAttributes(desktopIni, FileAttributes.Normal);
                        File.Delete(desktopIni);
                    }
                } catch { }

                try {
                    string panakJson = Path.Combine(folderPath, "panak.json");
                    if (File.Exists(panakJson)) {
                        File.SetAttributes(panakJson, FileAttributes.Normal);
                        File.Delete(panakJson);
                    }
                } catch { }

                try {
                    foreach (var ico in Directory.GetFiles(folderPath, "panak_icon*.ico")) {
                        File.SetAttributes(ico, FileAttributes.Normal);
                        File.Delete(ico);
                    }
                } catch { }

                try {
                    var folderAttrs = File.GetAttributes(folderPath);
                    File.SetAttributes(folderPath, FileAttributes.Normal);
                    Directory.Delete(folderPath, true);
                } catch { }

                ShellHelper.NotifyItemDeleted(folderPath, isDirectory: true);
                if (!string.IsNullOrEmpty(parentDir)) {
                    ShellHelper.NotifyFolderCreated(parentDir);
                }

                return true;
            } catch {
                return false;
            }
        }

        public void ScheduleFolderIconUpdate(string folderPath, bool immediate = false, string? shape = null, string? color = null) {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                return;
            }

            if (CheckAndDissolveFolder(folderPath)) {
                return;
            }

            if (immediate) {
                lock (_debounceLock) {
                    if (_debounceTimers.TryGetValue(folderPath, out var timer)) {
                        try {
                            timer.Dispose();
                        } catch { }
                        _debounceTimers.Remove(folderPath);
                    }
                    _lastUpdateTicks[folderPath] = Environment.TickCount64;
                }

                System.Threading.Tasks.Task.Run(() => {
                    try {
                        string iconShape = shape ?? string.Empty;
                        string iconColor = color ?? string.Empty;
                        var folderData = PanakFolderData.Load(folderPath);
                        iconShape = string.IsNullOrEmpty(iconShape) ? folderData.IconShape : iconShape;
                        iconColor = string.IsNullOrEmpty(iconColor) ? folderData.Color : iconColor;
                        IconGenerator.GenerateFolderIcon(folderPath, iconShape, iconColor, folderData.BackgroundOpacity);
                    } catch { }
                });
                return;
            }

            lock (_debounceLock) {
                if (_debounceTimers.TryGetValue(folderPath, out var existingTimer)) {
                    try {
                        existingTimer.Change(200, System.Threading.Timeout.Infinite);
                    } catch { }
                } else {
                    System.Threading.Timer? newTimer = null;
                    newTimer = new System.Threading.Timer(_ => {
                        lock (_debounceLock) {
                            if (_debounceTimers.TryGetValue(folderPath, out var t) && ReferenceEquals(t, newTimer)) {
                                _debounceTimers.Remove(folderPath);
                            }
                            newTimer?.Dispose();
                            _lastUpdateTicks[folderPath] = Environment.TickCount64;
                        }

                        if (!Directory.Exists(folderPath)) {
                            return;
                        }

                        if (CheckAndDissolveFolder(folderPath)) {
                            return;
                        }

                        try {
                            var folderData = PanakFolderData.Load(folderPath);
                            IconGenerator.GenerateFolderIcon(folderPath, folderData.IconShape, folderData.Color, folderData.BackgroundOpacity);
                        } catch { }
                    }, null, 200, System.Threading.Timeout.Infinite);

                    _debounceTimers[folderPath] = newTimer;
                }
            }
        }

        public void WatchFolder(string folderPath) {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                return;
            }

            lock (_watchers) {
                if (_watchers.ContainsKey(folderPath)) {
                    return;
                }

                try {
                    var watcher = new FileSystemWatcher(folderPath) {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };

                    void OnFileSystemEvent(object sender, FileSystemEventArgs e) {
                        if (string.IsNullOrEmpty(e.Name)) {
                            return;
                        }

                        string fileName = Path.GetFileName(e.Name);
                        if (fileName.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) ||
                            fileName.StartsWith("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                            fileName.StartsWith("panak.json", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) {
                            return;
                        }

                        ScheduleFolderIconUpdate(folderPath, immediate: false);

                        if (_isOpen && _popupWindow != null) {
                            Application.Current?.Dispatcher.BeginInvoke(() => {
                                if (_isOpen && _popupWindow != null) {
                                    _popupWindow.OnExternalFolderContentsChanged(folderPath);
                                }
                            });
                        }
                    }

                    watcher.Created += OnFileSystemEvent;
                    watcher.Deleted += OnFileSystemEvent;
                    watcher.Changed += OnFileSystemEvent;
                    watcher.Renamed += (s, e) => OnFileSystemEvent(s, e);

                    _watchers[folderPath] = watcher;
                    _knownFolders.Add(folderPath);
                } catch {
                    // Fallback
                }
            }
        }

        public void OpenFolder(string folderPath, Rect? iconBounds = null) {
            Application.Current.Dispatcher.Invoke(() => {
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                if (_isOpen && !string.IsNullOrEmpty(_currentOpenPath) &&
                    string.Equals(_currentOpenPath, folderPath, StringComparison.OrdinalIgnoreCase)) {
                    // Already open for this folder! Re-activate and bring to front instead of closing & reopening
                    if (_popupWindow != null && _popupWindow.IsVisible) {
                        _popupWindow.Activate();
                        _popupWindow.Focus();
                        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(_popupWindow).Handle;
                        ShellHelper.ForceForegroundWindow(hwnd);
                        return;
                    }
                }

                if (_isOpen) {
                    ClosePopup();
                }

                _isOpen = true;
                _currentOpenPath = folderPath;
                SetupParentWatcher(folderPath);
                if (CheckIfPanakFolder(folderPath)) {
                    _knownFolders.Add(folderPath);
                    WatchFolder(folderPath);
                }

                Rect bounds = iconBounds ?? Rect.Empty;
                if (bounds.IsEmpty) {
                    bounds = ExplorerHelper.GetBoundingBoxForPath(folderPath);
                }

                // Show popup window
                _popupWindow ??= new FolderPopupWindow();
                _popupWindow.ClosedAnimated -= OnPopupClosed;
                _popupWindow.ClosedAnimated += OnPopupClosed;
                _popupWindow.OpeningAnimationStarting -= OnPopupOpeningAnimationStarting;
                _popupWindow.OpeningAnimationStarting += OnPopupOpeningAnimationStarting;

                _popupWindow.LoadFolder(folderPath, bounds);

                // Ensure any concurrently opened Explorer window for this folder is closed
                CloseExplorerWindowForPath(folderPath);
            });
        }

        private void OnPopupOpeningAnimationStarting() {
            if (SettingsService.Instance.Current.FocusModeEnabled) {
                _backdropWindow ??= new BackdropWindow();
                _backdropWindow.ClickedOutside -= OnBackdropClicked;
                _backdropWindow.ClickedOutside += OnBackdropClicked;

                Rect workArea = SystemParameters.WorkArea;
                _backdropWindow.ShowBackdrop(workArea, SettingsService.Instance.Current.FocusDimmingOpacity);
            } else {
                _backdropWindow?.HideBackdrop();
            }
        }

        private static void CloseExplorerWindowForPath(string folderPath) {
            Task.Run(async () => {
                for (int attempt = 0; attempt < 3; attempt++) {
                    await Task.Delay(attempt == 0 ? 60 : 220);
                    try {
                        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                        if (shellType == null) {
                            return;
                        }
                        dynamic? shell = Activator.CreateInstance(shellType);
                        if (shell == null) {
                            return;
                        }
                        dynamic windows = shell.Windows();
                        int count = windows.Count;
                        for (int i = 0; i < count; i++) {
                            try {
                                dynamic item = windows.Item(i);
                                if (item == null) {
                                    continue;
                                }
                                string loc = item.LocationURL;
                                if (!string.IsNullOrEmpty(loc)) {
                                    Uri uri = new Uri(loc);
                                    if (uri.LocalPath.TrimEnd('\\').Equals(folderPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) {
                                        item.Quit();
                                    }
                                }
                            } catch {
                                // Ignore window iteration errors
                            }
                        }
                    } catch {
                        // Ignore shell COM errors
                    }
                }

                // Ensure popup re-asserts foreground window after background explorer window closure
                Application.Current?.Dispatcher.BeginInvoke(() => {
                    if (PanakManager.Instance.IsPopupOpen && PanakManager.Instance._popupWindow != null) {
                        IntPtr hwnd = PanakManager.Instance.GetPopupHwnd();
                        if (hwnd != IntPtr.Zero) {
                            ShellHelper.ForceForegroundWindow(hwnd);
                        }
                    }
                });
            });
        }

        public void ClosePopup() {
            Application.Current.Dispatcher.Invoke(() => {
                if (!_isOpen) {
                    return;
                }

                _popupWindow?.CloseAnimated();
                _backdropWindow?.HideBackdrop();
                _isOpen = false;
                _currentOpenPath = string.Empty;
                try {
                    _currentParentWatcher?.Dispose();
                    _currentParentWatcher = null;
                } catch { }
            });
        }

        private void OnPopupClosed() {
            _isOpen = false;
            _currentOpenPath = string.Empty;
            _backdropWindow?.HideBackdrop();
            try {
                _currentParentWatcher?.Dispose();
                _currentParentWatcher = null;
            } catch { }
        }

        private void SetupParentWatcher(string folderPath) {
            try {
                _currentParentWatcher?.Dispose();
                _currentParentWatcher = null;

                string? parent = Path.GetDirectoryName(folderPath);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) {
                    _currentParentWatcher = new FileSystemWatcher(parent) {
                        NotifyFilter = NotifyFilters.DirectoryName,
                        IncludeSubdirectories = false,
                        EnableRaisingEvents = true
                    };

                    _currentParentWatcher.Renamed += (s, e) => {
                        if (string.Equals(e.OldFullPath, _currentOpenPath, StringComparison.OrdinalIgnoreCase)) {
                            RenameFolder(e.OldFullPath, e.FullPath);
                            Application.Current?.Dispatcher.BeginInvoke(() => {
                                if (_isOpen && _popupWindow != null) {
                                    _popupWindow.OnExternalFolderRenamed(e.OldFullPath, e.FullPath);
                                }
                            });
                        }
                    };
                }
            } catch { }
        }

        public void RenameFolder(string oldPath, string newPath) {
            lock (_watchers) {
                if (_watchers.TryGetValue(oldPath, out var watcher)) {
                    try {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    } catch { }
                    _watchers.Remove(oldPath);
                }
            }

            lock (_knownFolders) {
                _knownFolders.Remove(oldPath);
                _knownFolders.Add(newPath);
            }

            lock (_debounceLock) {
                if (_debounceTimers.TryGetValue(oldPath, out var timer)) {
                    try { timer.Dispose(); } catch { }
                    _debounceTimers.Remove(oldPath);
                }
                _lastUpdateTicks.Remove(oldPath);
            }

            if (string.Equals(_currentOpenPath, oldPath, StringComparison.OrdinalIgnoreCase)) {
                _currentOpenPath = newPath;
            }

            WatchFolder(newPath);
        }

        public void UnwatchFolder(string folderPath) {
            lock (_watchers) {
                if (_watchers.TryGetValue(folderPath, out var watcher)) {
                    try {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    } catch { }
                    _watchers.Remove(folderPath);
                }
            }
        }

        private void OnBackdropClicked() {
            if (IsPopupWithinGracePeriod) {
                return;
            }
            ClosePopup();
        }

        public void ConvertToPanakFolder(string folderPath) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }
                var data = PanakFolderData.Load(folderPath);
                data.AutoPopup = true;
                data.Save(folderPath);

                _knownFolders.Add(folderPath);
                WatchFolder(folderPath);
                IconGenerator.GenerateFolderIcon(folderPath, data.IconShape, data.Color, data.BackgroundOpacity);
            } catch {
                // Ignore conversion errors
            }
        }

        public void RevertToRegularFolder(string folderPath) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }
                _knownFolders.Remove(folderPath);

                string metaFile = Path.Combine(folderPath, "panak.json");
                if (File.Exists(metaFile)) {
                    try {
                        File.Delete(metaFile);
                    } catch { }
                }

                string iniPath = Path.Combine(folderPath, "desktop.ini");
                if (File.Exists(iniPath)) {
                    try {
                        File.SetAttributes(iniPath, FileAttributes.Normal);
                        File.Delete(iniPath);
                    } catch { }
                }

                foreach (var ico in Directory.GetFiles(folderPath, "panak_icon*.ico")) {
                    try {
                        File.SetAttributes(ico, FileAttributes.Normal);
                        File.Delete(ico);
                    } catch { }
                }

                // Remove ReadOnly attribute from folder when reverting to regular Windows folder
                try {
                    var folderAttrs = File.GetAttributes(folderPath);
                    if (folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(folderPath, folderAttrs & ~FileAttributes.ReadOnly);
                    }
                } catch { }

                ShellHelper.NotifyShellItemChanged(folderPath);
            } catch {
                // Ignore cleanup errors
            }
        }

        public string CreatePanakFolder(string parentDir, string[]? filesToInclude = null, string? preferredName = null, Point? targetPosition = null) {
            try {
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) {
                    parentDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                string baseName = !string.IsNullOrEmpty(preferredName) ? preferredName : LocalizationService.Instance["NewFolder"];
                string targetPath = Path.Combine(parentDir, baseName);

                int counter = 1;
                while (Directory.Exists(targetPath) || File.Exists(targetPath)) {
                    targetPath = Path.Combine(parentDir, $"{baseName} ({counter++})");
                }

                Directory.CreateDirectory(targetPath);
                try {
                    var folderAttrs = File.GetAttributes(targetPath);
                    if (!folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(targetPath, folderAttrs | FileAttributes.ReadOnly);
                    }
                } catch { }

                var settings = SettingsService.Instance.Current;

                // Immediately create panak.json metadata
                var data = new PanakFolderData {
                    Title = Path.GetFileName(targetPath),
                    Color = settings.ThemeColor,
                    IconShape = settings.IconShape,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                string metaFile = Path.Combine(targetPath, "panak.json");
                File.WriteAllText(metaFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                File.SetAttributes(metaFile, FileAttributes.Hidden);

                _knownFolders.Add(targetPath);
                WatchFolder(targetPath);

                // Copy or move initial files
                if (filesToInclude != null && filesToInclude.Length > 0) {
                    foreach (var file in filesToInclude) {
                        try {
                            if (File.Exists(file)) {
                                string dest = Path.Combine(targetPath, Path.GetFileName(file));
                                File.Move(file, dest, true);
                            }
                        } catch {
                            // Ignore single file move failure
                        }
                    }
                }

                // Generate initial Android-style 2x2 preview icon
                IconGenerator.GenerateFolderIcon(targetPath, data.IconShape, data.Color, data.BackgroundOpacity);

                // Refresh Explorer and Desktop
                ShellHelper.NotifyFolderCreated(targetPath);

                // Position desktop icon at targetPosition if on desktop
                if (targetPosition != null) {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    if (parentDir.TrimEnd('\\').Equals(desktop.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                        parentDir.TrimEnd('\\').Equals(commonDesktop.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) {
                        ExplorerHelper.SetDesktopItemPosition(Path.GetFileName(targetPath), (int)targetPosition.Value.X, (int)targetPosition.Value.Y);
                    }
                }

                return targetPath;
            } catch {
                return string.Empty;
            }
        }

        public string CollectFilesIntoPanakFolder(string[] files, string? targetDir = null, Rect? bounds = null) {
            try {
                if (files == null || files.Length == 0) {
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) {
                    string first = files.FirstOrDefault(f => !string.IsNullOrEmpty(f)) ?? string.Empty;
                    if (!string.IsNullOrEmpty(first)) {
                        targetDir = Path.GetDirectoryName(first);
                    }
                    if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) {
                        targetDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }
                }

                Point? targetPos = null;
                if (bounds != null && !bounds.Value.IsEmpty) {
                    targetPos = new Point(bounds.Value.X + bounds.Value.Width / 2, bounds.Value.Y + bounds.Value.Height / 2);
                }

                string baseName = Path.GetFileNameWithoutExtension(files[0]);
                string newFolder = CreatePanakFolder(targetDir, files, baseName, targetPos);
                if (!string.IsNullOrEmpty(newFolder)) {
                    Rect openBounds = (bounds != null && !bounds.Value.IsEmpty)
                        ? bounds.Value
                        : ExplorerHelper.GetBoundingBoxForPath(newFolder);
                    OpenFolder(newFolder, openBounds);
                }
                return newFolder;
            } catch {
                return string.Empty;
            }
        }
    }
}
