using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Panak.Services {
    public class CrowdinManifest {
        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = new();

        [JsonPropertyName("languages")]
        public List<string> Languages { get; set; } = new();

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("content")]
        public Dictionary<string, List<string>> Content { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class CrowdinOtaService {
        private static CrowdinOtaService? _instance;
        public static CrowdinOtaService Instance => _instance ??= new CrowdinOtaService();

        private readonly HttpClient _httpClient;
        private readonly string _distributionHash;
        private readonly string _cacheDirectory;
        private CrowdinManifest? _cachedManifest;

        public const string DefaultDistributionHash = "44ab7f0051501158d306f9aoim1";

        public CrowdinOtaService(HttpClient? httpClient = null, string distributionHash = DefaultDistributionHash) {
            _httpClient = httpClient ?? new HttpClient();
            _distributionHash = distributionHash;
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Panak",
                "CrowdinOTA",
                _distributionHash
            );

            try {
                if (!Directory.Exists(_cacheDirectory)) {
                    Directory.CreateDirectory(_cacheDirectory);
                }
            } catch { }
        }

        private string BaseUrl => $"https://distributions.crowdin.net/{_distributionHash}";
        private string ManifestCachePath => Path.Combine(_cacheDirectory, "manifest.json");

        public Dictionary<string, string>? LoadCachedTranslations(string langCode) {
            try {
                var manifest = GetLocalManifest();
                string key = manifest != null ? ResolveLanguageKey(manifest, langCode) ?? langCode : langCode;
                string path = GetCacheFilePathForLang(key);

                if (File.Exists(path)) {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
            } catch { }
            return null;
        }

        public async Task<Dictionary<string, string>?> RefreshLanguageAsync(string langCode, CancellationToken ct = default) {
            return await CheckAndUpdateAsync(langCode, ct);
        }

        public async Task<Dictionary<string, string>?> CheckAndUpdateAsync(string langCode, CancellationToken ct = default) {
            try {
                var remoteManifest = await FetchRemoteManifestAsync(ct);
                if (remoteManifest == null) {
                    return null;
                }

                var localManifest = GetLocalManifest();
                string? matchedKey = ResolveLanguageKey(remoteManifest, langCode);
                if (matchedKey == null || !remoteManifest.Content.TryGetValue(matchedKey, out var filePaths) || filePaths.Count == 0) {
                    return null;
                }

                string targetFile = filePaths[0];
                string localFilePath = GetCacheFilePathForLang(matchedKey);

                if (localManifest != null && localManifest.Timestamp >= remoteManifest.Timestamp && File.Exists(localFilePath)) {
                    string cachedJson = await File.ReadAllTextAsync(localFilePath, ct);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(cachedJson);
                }

                string fileUrl = $"{BaseUrl}{targetFile}";
                using var response = await _httpClient.GetAsync(fileUrl, ct);
                if (!response.IsSuccessStatusCode) {
                    return null;
                }

                string jsonContent = await response.Content.ReadAsStringAsync(ct);
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                if (translations != null) {
                    await File.WriteAllTextAsync(localFilePath, jsonContent, ct);
                    await File.WriteAllTextAsync(ManifestCachePath, JsonSerializer.Serialize(remoteManifest), ct);
                    _cachedManifest = remoteManifest;
                }

                return translations;
            } catch {
                return null;
            }
        }

        private async Task<CrowdinManifest?> FetchRemoteManifestAsync(CancellationToken ct) {
            try {
                string url = $"{BaseUrl}/manifest.json";
                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<CrowdinManifest>(json);
            } catch {
                return null;
            }
        }

        private CrowdinManifest? GetLocalManifest() {
            if (_cachedManifest != null) {
                return _cachedManifest;
            }

            if (!File.Exists(ManifestCachePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(ManifestCachePath);
                _cachedManifest = JsonSerializer.Deserialize<CrowdinManifest>(json);
                return _cachedManifest;
            } catch {
                return null;
            }
        }

        private string GetCacheFilePathForLang(string langKey) {
            return Path.Combine(_cacheDirectory, $"{langKey}.json");
        }

        private static string? ResolveLanguageKey(CrowdinManifest manifest, string langCode) {
            if (manifest.Content.ContainsKey(langCode)) {
                return langCode;
            }

            foreach (var key in manifest.Content.Keys) {
                if (key.StartsWith(langCode, StringComparison.OrdinalIgnoreCase) ||
                    langCode.StartsWith(key, StringComparison.OrdinalIgnoreCase)) {
                    return key;
                }
            }

            return null;
        }
    }
}
