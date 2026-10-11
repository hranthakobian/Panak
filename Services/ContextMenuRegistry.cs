using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Panak.Services {
    public static class ContextMenuRegistry {
        private const string HKCU_CLASSES = @"Software\Classes";

        public static void RegisterAll(string exePath) {
            try {
                if (string.IsNullOrEmpty(exePath)) {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                }
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) {
                    return;
                }

                string createText = LocalizationService.Instance["CollectIntoPanakFolder"];
                string openText = LocalizationService.Instance["OpenInPanak"];
                string convertText = LocalizationService.Instance["ConvertToPanak"];

                // 1. HKCU\Software\Classes\*\shell\PanakCreate (Create Panak folder from file / selection)
                using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\*\shell\PanakCreate")) {
                    key.SetValue("", createText);
                    key.SetValue("MultiSelectModel", "Player");
                    key.SetValue("Icon", $"\"{exePath}\",0");
                    using var cmdKey = key.CreateSubKey("command");
                    cmdKey.SetValue("", $"\"{exePath}\" --create \"%1\"");
                }

                // 2. HKCU\Software\Classes\Directory\shell\PanakOpen and Folder\shell\PanakOpen (Open with Panak)
                string[] dirKeys = new[] { "Directory", "Folder" };
                foreach (var dk in dirKeys) {
                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{dk}\shell\PanakOpen")) {
                        key.SetValue("", openText);
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --open \"%1\"");
                    }

                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{dk}\shell\PanakConvert")) {
                        key.SetValue("", convertText);
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --convert \"%1\"");
                    }
                }

                // 3. HKCU\Software\Classes\Directory\Background\shell\PanakNew and DesktopBackground (New Panak folder in background)
                string[] bgKeys = new[] { @"Directory\Background", "DesktopBackground" };
                foreach (var bk in bgKeys) {
                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{bk}\shell\PanakNew")) {
                        key.SetValue("", createText);
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --new \"%V\"");
                    }
                }

                // 4. HKCU\Software\Classes\.panak association
                using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\.panak")) {
                    key.SetValue("", "PanakFolder");
                }

                using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\PanakFolder")) {
                    key.SetValue("", "Panak Android Folder");
                    using var defIcon = key.CreateSubKey("DefaultIcon");
                    defIcon.SetValue("", $"\"{exePath}\",0");
                    using var shell = key.CreateSubKey("shell");
                    using var open = shell.CreateSubKey("open");
                    using var cmd = open.CreateSubKey("command");
                    cmd.SetValue("", $"\"{exePath}\" --open \"%1\"");
                }

                ShellHelper.NotifyShellAssocChanged();
            } catch {
                // Ignore registry access error
            }
        }

        public static void UnregisterAll() {
            try {
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\*\shell\PanakCreate", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\shell\PanakOpen", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\shell\PanakConvert", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Folder\shell\PanakOpen", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Folder\shell\PanakConvert", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\Background\shell\PanakNew", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\DesktopBackground\shell\PanakNew", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\.panak", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\PanakFolder", false);

                ShellHelper.NotifyShellAssocChanged();
            } catch {
                // Ignore registry cleanup error
            }
        }

        public static bool IsRegistered() {
            try {
                using var key = Registry.CurrentUser.OpenSubKey($@"{HKCU_CLASSES}\*\shell\PanakCreate");
                return key != null;
            } catch {
                return false;
            }
        }
    }
}
