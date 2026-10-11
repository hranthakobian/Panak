using System;
using System.IO;
using System.Text.Json;
using Panak.Models;

namespace Panak.Services {
    public class SettingsService {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private readonly string _settingsFolder;
        private readonly string _settingsFilePath;

        public AppSettings Current { get; private set; }

        public event Action<AppSettings>? SettingsChanged;

        private SettingsService() {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsFolder = Path.Combine(appData, "Panak");
            _settingsFilePath = Path.Combine(_settingsFolder, "settings.json");

            Current = LoadSettings();
            LocalizationService.Instance.CurrentLanguage = Current.Language;
        }

        public AppSettings LoadSettings() {
            try {
                if (File.Exists(_settingsFilePath)) {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) {
                        return settings;
                    }
                }
            } catch {
                // fallback to defaults on read error
            }

            var defaultSettings = new AppSettings();
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        public void SaveSettings(AppSettings? settings = null) {
            try {
                if (settings != null) {
                    Current = settings;
                }

                if (!Directory.Exists(_settingsFolder)) {
                    Directory.CreateDirectory(_settingsFolder);
                }

                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                LocalizationService.Instance.CurrentLanguage = Current.Language;
                SettingsChanged?.Invoke(Current);
            } catch {
                // handle permission or write errors gracefully
            }
        }

        public void Save(AppSettings? settings = null) => SaveSettings(settings);
    }
}
