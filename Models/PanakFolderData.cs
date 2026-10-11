using System;
using System.Collections.Generic;
using Panak.Services;

namespace Panak.Models {
    public class PanakFolderData {
        public string Title { get; set; } = "Panak Folder";
        public string Color { get; set; } = ShellHelper.GetDefaultPastelColorHex();
        public string IconShape { get; set; } = "Squircle";
        public double BackgroundOpacity { get; set; } = 0.85;
        public string ThemeMode { get; set; } = "Auto";
        public bool AutoPopup { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<string> ItemNames { get; set; } = new List<string>();

        public static PanakFolderData Load(string folderPath) {
            string diskFolderName = System.IO.Path.GetFileName(folderPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(diskFolderName)) {
                diskFolderName = folderPath;
            }

            try {
                string metaFile = System.IO.Path.Combine(folderPath, "panak.json");
                if (System.IO.File.Exists(metaFile)) {
                    string json = System.IO.File.ReadAllText(metaFile);
                    var data = System.Text.Json.JsonSerializer.Deserialize<PanakFolderData>(json);
                    if (data != null) {
                        // Keep Title strictly synchronized with actual Windows folder name
                        if (!string.IsNullOrEmpty(diskFolderName) && !diskFolderName.Equals(data.Title, StringComparison.OrdinalIgnoreCase)) {
                            data.Title = diskFolderName;
                        }
                        if (data.BackgroundOpacity <= 0) {
                            data.BackgroundOpacity = 0.85;
                        }
                        if (string.IsNullOrEmpty(data.ThemeMode)) {
                            data.ThemeMode = "Auto";
                        }
                        return data;
                    }
                }
            } catch {
                // Ignore load error
            }

            return new PanakFolderData {
                Title = diskFolderName,
                Color = ShellHelper.GetDefaultPastelColorHex(),
                BackgroundOpacity = 0.85,
                ThemeMode = "Auto"
            };
        }

        public void Save(string folderPath) {
            try {
                if (!System.IO.Directory.Exists(folderPath)) {
                    return;
                }
                string metaFile = System.IO.Path.Combine(folderPath, "panak.json");
                UpdatedAt = DateTime.UtcNow;
                string json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                for (int attempt = 0; attempt < 5; attempt++) {
                    try {
                        if (System.IO.File.Exists(metaFile)) {
                            // Open existing file and truncate to write new content without failing on Hidden attribute
                            using var fs = new System.IO.FileStream(metaFile, System.IO.FileMode.Open, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
                            fs.SetLength(0);
                            using var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);
                            sw.Write(json);
                            sw.Flush();
                        } else {
                            using var fs = new System.IO.FileStream(metaFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
                            using var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);
                            sw.Write(json);
                            sw.Flush();
                        }

                        try {
                            var attrs = System.IO.File.GetAttributes(metaFile);
                            if (!attrs.HasFlag(System.IO.FileAttributes.Hidden)) {
                                System.IO.File.SetAttributes(metaFile, attrs | System.IO.FileAttributes.Hidden);
                            }
                        } catch { }
                        return;
                    } catch (System.IO.IOException) {
                        System.Threading.Thread.Sleep(50);
                    } catch (System.UnauthorizedAccessException) {
                        try {
                            // If locked or read-only, ensure normal write access temporarily
                            var attrs = System.IO.File.GetAttributes(metaFile);
                            System.IO.File.SetAttributes(metaFile, attrs & ~System.IO.FileAttributes.ReadOnly);
                        } catch { }
                        System.Threading.Thread.Sleep(50);
                    }
                }
            } catch {
                // Ignore save error
            }
        }
    }
}
