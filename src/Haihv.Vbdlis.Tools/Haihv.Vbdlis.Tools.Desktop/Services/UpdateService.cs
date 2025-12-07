using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for checking and downloading application updates from GitHub Releases
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private const string GitHubRepoOwner = "haitnmt";
        private const string GitHubRepoName = "Vbdlis-Tools";

        public string CurrentVersion { get; }

        public UpdateService()
        {
            _logger = Log.ForContext<UpdateService>();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VbdlisTools-UpdateChecker");

            // Get current version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

            _logger.Information("UpdateService initialized. Current version: {Version}", CurrentVersion);
        }

        /// <summary>
        /// Checks GitHub Releases for new version
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                _logger.Information("Checking for updates...");

                var apiUrl = $"https://api.github.com/repos/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";
                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warning("Failed to check for updates. Status: {Status}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var latestVersion = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
                var publishedAt = root.GetProperty("published_at").GetDateTime();
                var releaseNotes = root.GetProperty("body").GetString() ?? "";

                // Compare versions
                if (!IsNewerVersion(latestVersion, CurrentVersion))
                {
                    _logger.Information("Already on latest version: {Version}", CurrentVersion);
                    return null;
                }

                // Find Windows installer asset
                string? downloadUrl = null;
                long fileSize = 0;

                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";

                        // Look for setup.exe or .msi file
                        if (name.EndsWith("setup.exe", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            fileSize = asset.GetProperty("size").GetInt64();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _logger.Warning("No Windows installer found in release");
                    return null;
                }

                var updateInfo = new UpdateInfo
                {
                    Version = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes,
                    PublishedAt = publishedAt,
                    FileSize = fileSize,
                    IsRequired = false // Có thể cấu hình từ release notes
                };

                _logger.Information("Update available: {Version} -> {NewVersion}", CurrentVersion, latestVersion);
                return updateInfo;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking for updates");
                return null;
            }
        }

        /// <summary>
        /// Downloads and installs the update
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, Action<int>? progress = null)
        {
            try
            {
                _logger.Information("Downloading update from: {Url}", updateInfo.DownloadUrl);

                // Download to temp folder
                var tempPath = Path.Combine(Path.GetTempPath(), "VbdlisTools-Update");
                Directory.CreateDirectory(tempPath);

                var fileName = Path.GetFileName(new Uri(updateInfo.DownloadUrl).LocalPath);
                var filePath = Path.Combine(tempPath, fileName);

                // Download with progress
                using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSize;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercent = (int)((totalRead * 100) / totalBytes);
                            progress?.Invoke(progressPercent);
                        }
                    }
                }

                _logger.Information("Download completed: {Path}", filePath);

                // Launch installer and exit current application
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true,
                        Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS" // Inno Setup silent args
                    };

                    Process.Start(startInfo);

                    _logger.Information("Installer launched. Application will exit.");
                    return true;
                }
                else
                {
                    _logger.Warning("Auto-install not supported on this platform");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error downloading/installing update");
                return false;
            }
        }

        /// <summary>
        /// Compares two version strings
        /// </summary>
        private bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                var newVer = Version.Parse(newVersion);
                var curVer = Version.Parse(currentVersion);
                return newVer > curVer;
            }
            catch
            {
                return false;
            }
        }
    }
}
