using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Panak.Services {
    public static class StandaloneInstallerService {
        public const string APP_NAME = "Panak";
        public const string UNINSTALL_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Panak";
        public const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string GetDefaultInstallDirectory() {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", APP_NAME);
        }

        public static bool IsInstalled(string? installDir = null) {
            try {
                using var key = Registry.CurrentUser.OpenSubKey(UNINSTALL_REG_KEY);
                if (key != null) return true;

                string checkDir = installDir ?? GetDefaultInstallDirectory();
                return File.Exists(Path.Combine(checkDir, "Panak.exe"));
            } catch {
                return false;
            }
        }

        public static string GetInstalledDirectory() {
            try {
                using var key = Registry.CurrentUser.OpenSubKey(UNINSTALL_REG_KEY);
                string? loc = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc)) {
                    return loc;
                }
            } catch { }
            return GetDefaultInstallDirectory();
        }

        public static void TerminateRunningPanak() {
            int currentPid = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("Panak")) {
                if (p.Id != currentPid) {
                    try {
                        p.Kill();
                        p.WaitForExit(2000);
                    } catch { }
                }
            }
        }

        public static async Task<(bool success, string message)> InstallAsync(
            string targetDir,
            bool createDesktopShortcut,
            bool createStartMenuShortcut,
            bool autoStart,
            IProgress<(double percent, string status)>? progress = null) {

            return await Task.Run(() => {
                try {
                    progress?.Report((0.05, LocalizationService.Instance["InstallDesc"] ?? "Preparing installation..."));

                    if (string.IsNullOrWhiteSpace(targetDir)) {
                        targetDir = GetDefaultInstallDirectory();
                    }

                    if (!Directory.Exists(targetDir)) {
                        Directory.CreateDirectory(targetDir);
                    }

                    // 1. Terminate existing instances
                    progress?.Report((0.15, "Stopping running Panak processes..."));
                    TerminateRunningPanak();

                    // 2. Extract embedded payload zip or fallback to local directory files
                    progress?.Report((0.25, "Extracting application files..."));
                    bool extractedFromPayload = TryExtractEmbeddedPayload(targetDir, progress);

                    if (!extractedFromPayload) {
                        // Fallback to copying files from adjacent or publish folder
                        CopyLocalFilesToTarget(targetDir, progress);
                    }

                    string targetExe = Path.Combine(targetDir, "Panak.exe");
                    if (!File.Exists(targetExe)) {
                        return (false, "Panak.exe was not found in destination directory.");
                    }

                    // 3. Copy Setup.exe itself to targetDir for integrated uninstaller support
                    try {
                        string currentExe = Environment.ProcessPath
                            ?? Process.GetCurrentProcess().MainModule?.FileName
                            ?? string.Empty;
                        if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe)) {
                            string setupTarget = Path.Combine(targetDir, "Setup.exe");
                            if (!currentExe.Equals(setupTarget, StringComparison.OrdinalIgnoreCase)) {
                                File.Copy(currentExe, setupTarget, true);
                            }
                        }
                    } catch { }

                    // 4. Create Shortcuts
                    progress?.Report((0.75, LocalizationService.Instance["DesktopShortcut"] ?? "Creating shortcuts..."));
                    if (createDesktopShortcut) {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string shortcutPath = Path.Combine(desktopPath, "Panak.lnk");
                        CreateShortcut(shortcutPath, targetExe, "--background", "Panak - Android-style Folders");
                    }

                    if (createStartMenuShortcut) {
                        string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        string shortcutPath = Path.Combine(startMenu, "Panak.lnk");
                        CreateShortcut(shortcutPath, targetExe, "--background", "Panak - Android-style Folders");
                    }

                    // 5. Register Context Menu in Windows Explorer
                    progress?.Report((0.85, LocalizationService.Instance["Integration"] ?? "Registering Explorer context menus..."));
                    ContextMenuRegistry.RegisterAll(targetExe);

                    // 6. Auto-start with Windows
                    progress?.Report((0.90, LocalizationService.Instance["AutoStart"] ?? "Configuring auto-start..."));
                    SetAutoStart(targetExe, autoStart);

                    // 7. Windows Add/Remove Programs (Registry)
                    progress?.Report((0.95, "Registering application..."));
                    string setupExeInTarget = Path.Combine(targetDir, "Setup.exe");
                    string uninstallCommand = File.Exists(setupExeInTarget)
                        ? $"\"{setupExeInTarget}\" --uninstall"
                        : $"\"{targetExe}\" --uninstall";

                    using (var key = Registry.CurrentUser.CreateSubKey(UNINSTALL_REG_KEY)) {
                        key.SetValue("DisplayName", "Panak - Material You Folders");
                        key.SetValue("DisplayIcon", $"\"{targetExe}\",0");
                        key.SetValue("DisplayVersion", "1.7.1");
                        key.SetValue("Publisher", "Panak Team");
                        key.SetValue("InstallLocation", targetDir);
                        key.SetValue("UninstallString", uninstallCommand);
                        key.SetValue("QuietUninstallString", $"{uninstallCommand} --silent");
                    }

                    progress?.Report((1.0, LocalizationService.Instance["InstalledSuccess"] ?? "Installation completed successfully!"));
                    return (true, string.Empty);
                } catch (Exception ex) {
                    Debug.WriteLine($"Install error: {ex}");
                    return (false, ex.Message);
                }
            });
        }

        public static async Task<(bool success, string message)> UninstallAsync(
            string? targetDir = null,
            IProgress<(double percent, string status)>? progress = null) {

            return await Task.Run(() => {
                try {
                    progress?.Report((0.10, "Stopping Panak processes..."));
                    TerminateRunningPanak();

                    progress?.Report((0.30, "Removing Explorer context menus..."));
                    ContextMenuRegistry.UnregisterAll();

                    progress?.Report((0.50, "Removing shortcuts..."));
                    // Remove shortcuts
                    string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Panak.lnk");
                    if (File.Exists(desktopShortcut)) {
                        try { File.Delete(desktopShortcut); } catch { }
                    }

                    string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Panak.lnk");
                    if (File.Exists(startMenuShortcut)) {
                        try { File.Delete(startMenuShortcut); } catch { }
                    }

                    progress?.Report((0.70, "Removing registry settings..."));
                    SetAutoStart(string.Empty, false);

                    try {
                        Registry.CurrentUser.DeleteSubKeyTree(UNINSTALL_REG_KEY, false);
                    } catch { }

                    progress?.Report((0.85, "Cleaning program files..."));
                    string installDir = !string.IsNullOrEmpty(targetDir) ? targetDir : GetInstalledDirectory();
                    string currentExe = Environment.ProcessPath
                        ?? Process.GetCurrentProcess().MainModule?.FileName
                        ?? string.Empty;

                    if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir)) {
                        if (currentExe.StartsWith(installDir, StringComparison.OrdinalIgnoreCase)) {
                            // If running Setup.exe from the install directory itself, schedule background folder deletion
                            ProcessStartInfo psi = new ProcessStartInfo {
                                FileName = "cmd.exe",
                                Arguments = $"/C ping 127.0.0.1 -n 3 > NUL & rmdir /S /Q \"{installDir}\"",
                                WindowStyle = ProcessWindowStyle.Hidden,
                                CreateNoWindow = true
                            };
                            Process.Start(psi);
                        } else {
                            // Running from an external standalone Setup.exe, we can safely delete the directory
                            try {
                                Directory.Delete(installDir, true);
                            } catch {
                                ProcessStartInfo psi = new ProcessStartInfo {
                                    FileName = "cmd.exe",
                                    Arguments = $"/C ping 127.0.0.1 -n 2 > NUL & rmdir /S /Q \"{installDir}\"",
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    CreateNoWindow = true
                                };
                                Process.Start(psi);
                            }
                        }
                    }

                    progress?.Report((1.0, LocalizationService.Instance["UninstalledSuccess"] ?? "Uninstallation completed successfully!"));
                    return (true, string.Empty);
                } catch (Exception ex) {
                    Debug.WriteLine($"Uninstall error: {ex}");
                    return (false, ex.Message);
                }
            });
        }

        private static bool TryExtractEmbeddedPayload(string targetDir, IProgress<(double percent, string status)>? progress) {
            try {
                var assembly = Assembly.GetExecutingAssembly();
                string? resName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("PanakPayload.zip", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(resName)) {
                    return false;
                }

                using var stream = assembly.GetManifestResourceStream(resName);
                if (stream == null) return false;

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                int totalEntries = archive.Entries.Count;
                if (totalEntries == 0) return false;

                int currentIndex = 0;
                foreach (var entry in archive.Entries) {
                    currentIndex++;
                    if (string.IsNullOrEmpty(entry.Name)) {
                        // Directory entry
                        string dirPath = Path.Combine(targetDir, entry.FullName);
                        if (!Directory.Exists(dirPath)) {
                            Directory.CreateDirectory(dirPath);
                        }
                        continue;
                    }

                    string destPath = Path.Combine(targetDir, entry.FullName);
                    string? dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }

                    entry.ExtractToFile(destPath, overwrite: true);
                    double pct = 0.25 + (0.45 * currentIndex / totalEntries);
                    progress?.Report((pct, $"Extracting: {entry.Name}"));
                }

                return true;
            } catch (Exception ex) {
                Debug.WriteLine($"Payload extraction error: {ex}");
                return false;
            }
        }

        private static void CopyLocalFilesToTarget(string targetDir, IProgress<(double percent, string status)>? progress) {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string? sourceDir = null;

            string[] candidates = new[] {
                Path.Combine(baseDir, "publish"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "publish")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Release", "net8.0-windows10.0.19041.0")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows10.0.19041.0")),
                baseDir
            };

            foreach (var cand in candidates) {
                if (Directory.Exists(cand) && File.Exists(Path.Combine(cand, "Panak.exe"))) {
                    sourceDir = cand;
                    break;
                }
            }

            if (sourceDir == null || sourceDir.Equals(targetDir, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
            int count = files.Length;
            for (int i = 0; i < count; i++) {
                string src = files[i];
                string rel = Path.GetRelativePath(sourceDir, src);
                string dest = Path.Combine(targetDir, rel);

                string? dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }

                try {
                    File.Copy(src, dest, true);
                } catch { }

                double pct = 0.25 + (0.45 * (i + 1) / count);
                progress?.Report((pct, $"Copying: {Path.GetFileName(src)}"));
            }

            // Ensure Panak.ShellExt files are also copied if present in build directory
            string[] shellExtCandidates = new[] {
                Path.Combine(baseDir, "Panak.ShellExt.comhost.dll"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Panak.ShellExt", "bin", "Release", "net8.0-windows", "Panak.ShellExt.comhost.dll")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Panak.ShellExt", "bin", "Debug", "net8.0-windows", "Panak.ShellExt.comhost.dll"))
            };
            foreach (var cand in shellExtCandidates) {
                if (File.Exists(cand)) {
                    string candDir = Path.GetDirectoryName(cand)!;
                    foreach (var f in Directory.GetFiles(candDir, "Panak.ShellExt.*")) {
                        string df = Path.Combine(targetDir, Path.GetFileName(f));
                        try { File.Copy(f, df, true); } catch { }
                    }
                    break;
                }
            }
        }

        public static void SetAutoStart(string exePath, bool enable) {
            try {
                if (string.IsNullOrEmpty(exePath)) {
                    string targetDir = GetDefaultInstallDirectory();
                    string candidateExe = Path.Combine(targetDir, "Panak.exe");
                    exePath = File.Exists(candidateExe)
                        ? candidateExe
                        : (Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
                }

                // 1. Registry HKCU Run Key
                using (var key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, true)) {
                    if (key != null) {
                        if (enable && !string.IsNullOrEmpty(exePath)) {
                            key.SetValue(APP_NAME, $"\"{exePath}\" --background");
                        } else {
                            key.DeleteValue(APP_NAME, false);
                        }
                    }
                }

                // 2. Windows Startup Folder Shortcut (shell:startup)
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string startupShortcut = Path.Combine(startupFolder, "Panak.lnk");
                if (enable && !string.IsNullOrEmpty(exePath) && File.Exists(exePath)) {
                    CreateShortcut(startupShortcut, exePath, "--background", "Panak - Android-style Folders");
                } else if (!enable && File.Exists(startupShortcut)) {
                    try {
                        File.Delete(startupShortcut);
                    } catch { }
                }
            } catch { }
        }

        public static bool IsAutoStartEnabled() {
            try {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY);
                bool regOk = key?.GetValue(APP_NAME) != null;
                string startupShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Panak.lnk");
                return regOk || File.Exists(startupShortcut);
            } catch {
                return false;
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description) {
            try {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null) {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null) {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = targetPath;
                        shortcut.Arguments = arguments;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                        shortcut.Description = description;
                        shortcut.IconLocation = $"{targetPath},0";
                        shortcut.Save();
                    }
                }
            } catch { }
        }
    }
}
