using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Panak.Models;

namespace Panak.Services {
    public enum UpdateStatus {
        Idle,
        Checking,
        UpToDate,
        UpdateAvailable,
        Downloading,
        ReadyToInstall,
        Error
    }

    public class GitHubUpdateService : IDisposable {
        public const string TargetRepository = "hranthakobian/Panak";
        public const string TargetRepositoryUrl = "https://github.com/hranthakobian/Panak";

        private static GitHubUpdateService? _instance;
        public static GitHubUpdateService Instance => _instance ??= new GitHubUpdateService();

        public static string NormalizeRepo(string? repo) {
            if (string.IsNullOrWhiteSpace(repo)) {
                return TargetRepository;
            }
            repo = repo.Trim();
            if (repo.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring("https://github.com/".Length);
            } else if (repo.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring("http://github.com/".Length);
            } else if (repo.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring("github.com/".Length);
            }
            repo = repo.Trim('/');
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring(0, repo.Length - 4);
            }
            var parts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) {
                return $"{parts[0]}/{parts[1]}";
            }
            return TargetRepository;
        }

        private readonly HttpClient _httpClient;
        private System.Threading.Timer? _periodicTimer;
        private bool _isDisposed = false;

        public UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
        public string CurrentVersionString { get; }
        public Version CurrentVersion { get; }
        public string? LatestVersionString { get; private set; }
        public Version? LatestVersion { get; private set; }
        public string? ReleaseTitle { get; private set; }
        public string? ReleaseNotes { get; private set; }
        public DateTime? PublishedAt { get; private set; }
        public string? DownloadUrl { get; private set; }
        public double DownloadProgress { get; private set; } = 0.0;
        public string? ErrorMessage { get; private set; }
        public string? DownloadedFilePath { get; private set; }

        public event Action<UpdateStatus, string?>? StatusChanged;
        public event Action<double>? DownloadProgressChanged;
        public event Action<string, string>? UpdateAvailableEvent;

        public GitHubUpdateService() {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            CurrentVersion = ver != null ? new Version(ver.Major, Math.Max(0, ver.Minor), Math.Max(0, ver.Build)) : new Version(1, 5, 5);
            CurrentVersionString = $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Panak-Updater", CurrentVersionString));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void Start() {
            if (_periodicTimer != null) {
                return;
            }

            // Initial check 10 seconds after launch, then every 60 minutes
            _periodicTimer = new System.Threading.Timer(_ => {
                if (SettingsService.Instance.Current.AutoCheckUpdates) {
                    _ = CheckForUpdatesAsync(manual: false);
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(60));
        }

        public async Task<bool> CheckForUpdatesAsync(bool manual = false) {
            if (Status == UpdateStatus.Checking || Status == UpdateStatus.Downloading) {
                return false;
            }

            SetStatus(UpdateStatus.Checking);
            ErrorMessage = null;

            string rawRepo = SettingsService.Instance.Current.GitHubRepo;
            string repo = string.IsNullOrWhiteSpace(rawRepo) ? TargetRepository : NormalizeRepo(rawRepo);

            try {
                string url = $"https://api.github.com/repos/{repo}/releases/latest";
                using var response = await _httpClient.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
                    // No releases found yet on GitHub
                    SetStatus(UpdateStatus.UpToDate, "No releases published on GitHub repository yet.");
                    SaveLastCheckTime();
                    return false;
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string rawTag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string tagName = rawTag.TrimStart('v', 'V');
                ReleaseTitle = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : rawTag;
                ReleaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";

                if (root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var pubDate)) {
                    PublishedAt = pubDate;
                }

                // Look for release assets (installer exe or zip)
                DownloadUrl = null;
                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array) {
                    foreach (var asset in assetsProp.EnumerateArray()) {
                        string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                        string assetUrl = asset.TryGetProperty("browser_download_url", out var aUrl) ? aUrl.GetString() ?? "" : "";

                        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                            DownloadUrl = assetUrl;
                            break;
                        }
                    }
                }

                // Fallback to zipball if no specific asset
                if (string.IsNullOrEmpty(DownloadUrl) && root.TryGetProperty("zipball_url", out var zipProp)) {
                    DownloadUrl = zipProp.GetString();
                }

                LatestVersionString = tagName;
                if (Version.TryParse(tagName, out var parsedVersion)) {
                    LatestVersion = parsedVersion;
                } else if (tagName.Contains('.')) {
                    string[] parts = tagName.Split('.');
                    int major = parts.Length > 0 && int.TryParse(parts[0], out int mj) ? mj : 1;
                    int minor = parts.Length > 1 && int.TryParse(parts[1], out int mn) ? mn : 0;
                    int build = parts.Length > 2 && int.TryParse(parts[2], out int bd) ? bd : 0;
                    LatestVersion = new Version(major, minor, build);
                } else {
                    LatestVersion = new Version(1, 0, 0);
                }

                SaveLastCheckTime();

                if (LatestVersion > CurrentVersion) {
                    SetStatus(UpdateStatus.UpdateAvailable);
                    Application.Current?.Dispatcher.BeginInvoke(() => {
                        UpdateAvailableEvent?.Invoke(LatestVersionString ?? "New", ReleaseNotes ?? "");
                    });
                    return true;
                } else {
                    SetStatus(UpdateStatus.UpToDate);
                    return false;
                }
            } catch (Exception ex) {
                ErrorMessage = ex.Message;
                SetStatus(UpdateStatus.Error, ex.Message);
                return false;
            }
        }

        public async Task<bool> DownloadAndApplyUpdateAsync(Action<double>? progressCallback = null) {
            if (string.IsNullOrEmpty(DownloadUrl)) {
                SetStatus(UpdateStatus.Error, "No download link available for this release.");
                return false;
            }

            SetStatus(UpdateStatus.Downloading);
            DownloadProgress = 0.0;
            DownloadProgressChanged?.Invoke(0.0);

            try {
                string tempDir = Path.Combine(Path.GetTempPath(), "PanakUpdate");
                if (!Directory.Exists(tempDir)) {
                    Directory.CreateDirectory(tempDir);
                }

                string ext = Path.GetExtension(DownloadUrl);
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) {
                    ext = ".exe";
                }
                string destFile = Path.Combine(tempDir, $"Panak_Update_{LatestVersionString ?? "latest"}{ext}");

                using (var response = await _httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead)) {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes.HasValue && totalBytes.Value > 0) {
                            double pct = (double)totalRead / totalBytes.Value * 100.0;
                            DownloadProgress = Math.Round(pct, 1);
                            progressCallback?.Invoke(DownloadProgress);
                            DownloadProgressChanged?.Invoke(DownloadProgress);
                        }
                    }
                }

                DownloadedFilePath = destFile;
                SetStatus(UpdateStatus.ReadyToInstall);

                // Launch installer or apply update
                ApplyDownloadedUpdate(destFile);
                return true;
            } catch (Exception ex) {
                ErrorMessage = ex.Message;
                SetStatus(UpdateStatus.Error, ex.Message);
                return false;
            }
        }

        private void ApplyDownloadedUpdate(string filePath) {
            try {
                if (filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                    // Launch downloaded installer
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = filePath,
                        Arguments = "--install-silent",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    Application.Current?.Dispatcher.BeginInvoke(() => {
                        Application.Current.Shutdown();
                    });
                } else if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                    // Extract zip to temp and run installer
                    string extractDir = Path.Combine(Path.GetDirectoryName(filePath)!, "Extracted");
                    if (Directory.Exists(extractDir)) {
                        Directory.Delete(extractDir, true);
                    }
                    ZipFile.ExtractToDirectory(filePath, extractDir);

                    string[] exeCandidates = Directory.GetFiles(extractDir, "Panak*.exe", SearchOption.AllDirectories);
                    if (exeCandidates.Length > 0) {
                        Process.Start(new ProcessStartInfo {
                            FileName = exeCandidates[0],
                            Arguments = "--install-silent",
                            UseShellExecute = true
                        });
                        Application.Current?.Dispatcher.BeginInvoke(() => {
                            Application.Current.Shutdown();
                        });
                    }
                }
            } catch (Exception ex) {
                SetStatus(UpdateStatus.Error, $"Failed to launch update: {ex.Message}");
            }
        }

        private void SetStatus(UpdateStatus newStatus, string? message = null) {
            Status = newStatus;
            if (message != null) {
                ErrorMessage = message;
            }
            Application.Current?.Dispatcher.BeginInvoke(() => {
                StatusChanged?.Invoke(newStatus, message ?? ErrorMessage);
            });
        }

        private static void SaveLastCheckTime() {
            try {
                SettingsService.Instance.Current.LastUpdateCheck = DateTime.UtcNow;
                SettingsService.Instance.SaveSettings();
            } catch { }
        }

        public void Dispose() {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;
            _periodicTimer?.Dispose();
            _periodicTimer = null;
            _httpClient?.Dispose();
        }
    }
}
