using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for checking and downloading application updates using Velopack
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private readonly ILogger _logger;
        private readonly UpdateManager? _updateManager;
        private const string GitHubRepoOwner = "haitnmt";
        private const string GitHubRepoName = "Vbdlis-Tools";

        // Cache the Velopack UpdateInfo to avoid re-checking
        private Velopack.UpdateInfo? _cachedVelopackUpdateInfo;

        public string CurrentVersion { get; }

        public UpdateService()
        {
            _logger = Log.ForContext<UpdateService>();

            // Get current version from assembly - use InformationalVersion (3-part) for Velopack compatibility
            var assembly = Assembly.GetExecutingAssembly();
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(infoVersion))
            {
                // Remove git commit hash if present (e.g., "1.0.25120911+01403dd..." -> "1.0.25120911")
                var plusIndex = infoVersion.IndexOf('+');
                CurrentVersion = plusIndex > 0 ? infoVersion.Substring(0, plusIndex) : infoVersion;
            }
            else
            {
                // Fallback to AssemblyVersion
                var version = assembly.GetName().Version;
                CurrentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
            }

            _logger.Information("UpdateService initialized. Current version: {Version}", CurrentVersion);

            // Initialize Velopack UpdateManager
            try
            {
                // Use GitHub as update source
                var source = new GithubSource($"https://github.com/{GitHubRepoOwner}/{GitHubRepoName}", null, false);
                _updateManager = new UpdateManager(source);
                _logger.Information("Velopack UpdateManager initialized with GitHub source");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to initialize Velopack UpdateManager. Updates will be disabled.");
                _updateManager = null;
            }
        }


        /// <summary>
        /// Checks for new version using Velopack
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            _logger.Information("[UPDATE] Kiểm tra cập nhật - Phiên bản hiện tại: {CurrentVersion}", CurrentVersion);

            if (_updateManager == null)
            {
                _logger.Warning("[UPDATE] UpdateManager chưa khởi tạo");
                return null;
            }

            try
            {
                var isInstalled = _updateManager.IsInstalled;
                _logger.Information("[UPDATE] IsInstalled = {IsInstalled}", isInstalled);

                if (!isInstalled)
                {
                    _logger.Warning("[UPDATE] App đang chạy portable mode - Auto-update chỉ hoạt động khi cài qua installer");
                    return null;
                }

                // Log detailed info before calling Velopack
                try
                {
                    var currentDir = Environment.CurrentDirectory;
                    var exePath = Environment.ProcessPath;
                    _logger.Information("[UPDATE] Current Directory: {CurrentDir}", currentDir);
                    _logger.Information("[UPDATE] Process Path: {ExePath}", exePath);
                    _logger.Information("[UPDATE] OS: {OS}", RuntimeInformation.OSDescription);
                }
                catch (Exception locEx)
                {
                    _logger.Warning(locEx, "[UPDATE] Không thể lấy thông tin app location");
                }

                _logger.Information("[UPDATE] Kết nối GitHub: https://github.com/{Owner}/{Repo}", GitHubRepoOwner, GitHubRepoName);
                _logger.Information("[UPDATE] Đang gọi _updateManager.CheckForUpdatesAsync()...");

                Velopack.UpdateInfo? updateInfo = null;
                try
                {
                    updateInfo = await _updateManager.CheckForUpdatesAsync();
                    _logger.Information("[UPDATE] CheckForUpdatesAsync() hoàn thành - Result: {IsNull}", updateInfo == null ? "NULL" : "NOT NULL");
                }
                catch (System.Net.Http.HttpRequestException httpEx)
                {
                    _logger.Error(httpEx, "[UPDATE] HTTP Error - Không kết nối được GitHub");
                    _logger.Error("[UPDATE] StatusCode: {StatusCode}", httpEx.StatusCode);
                    _logger.Error("[UPDATE] Message: {Message}", httpEx.Message);
                    if (httpEx.InnerException != null)
                    {
                        _logger.Error("[UPDATE] InnerException: {InnerMessage}", httpEx.InnerException.Message);
                    }
                    throw;
                }
                catch (TaskCanceledException timeoutEx)
                {
                    _logger.Error(timeoutEx, "[UPDATE] Timeout - Mất quá nhiều thời gian kết nối GitHub");
                    throw;
                }
                catch (Exception veloEx)
                {
                    _logger.Error(veloEx, "[UPDATE] Velopack Exception: {Type}", veloEx.GetType().Name);
                    _logger.Error("[UPDATE] Message: {Message}", veloEx.Message);
                    _logger.Error("[UPDATE] StackTrace: {StackTrace}", veloEx.StackTrace);
                    if (veloEx.InnerException != null)
                    {
                        _logger.Error("[UPDATE] InnerException: {InnerType} - {InnerMessage}",
                            veloEx.InnerException.GetType().Name,
                            veloEx.InnerException.Message);
                    }
                    throw;
                }

                if (updateInfo == null)
                {
                    _logger.Information("[UPDATE] Không có update (updateInfo == null) - Có thể đã là phiên bản mới nhất hoặc không tìm thấy RELEASES file");
                    return null;
                }

                var newVersion = updateInfo.TargetFullRelease.Version.ToString();
                var sizeMB = updateInfo.TargetFullRelease.Size / 1024.0 / 1024.0;

                _logger.Information("[UPDATE] Tìm thấy bản cập nhật: {CurrentVersion} → {NewVersion} (~{SizeMB:N1} MB)",
                    CurrentVersion, newVersion, sizeMB);

                // Cache the Velopack UpdateInfo for DownloadAndInstallUpdateAsync
                _cachedVelopackUpdateInfo = updateInfo;

                return new UpdateInfo
                {
                    Version = newVersion,
                    DownloadUrl = "", // Velopack handles download internally
                    ReleaseNotes = "", // Could be fetched from GitHub API separately if needed
                    PublishedAt = DateTime.Now, // Velopack doesn't provide this
                    FileSize = updateInfo.TargetFullRelease.Size,
                    IsRequired = false
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[UPDATE] Lỗi khi kiểm tra cập nhật: {Message}", ex.Message);
                return null;
            }
        }


        /// <summary>
        /// Downloads and installs the update using Velopack
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, Action<int>? progress = null)
        {
            _logger.Information("[UPDATE] Bắt đầu tải cập nhật: {Version}", updateInfo.Version);

            if (_updateManager == null)
            {
                _logger.Warning("[UPDATE] UpdateManager chưa khởi tạo");
                return false;
            }

            try
            {
                Velopack.UpdateInfo? velopackUpdateInfo;

                // Use cached UpdateInfo if available, otherwise check again
                if (_cachedVelopackUpdateInfo != null)
                {
                    _logger.Information("[UPDATE] Sử dụng thông tin cached");
                    velopackUpdateInfo = _cachedVelopackUpdateInfo;
                    _cachedVelopackUpdateInfo = null; // Clear cache after use
                }
                else
                {
                    _logger.Information("[UPDATE] Kiểm tra lại từ Velopack...");
                    velopackUpdateInfo = await _updateManager.CheckForUpdatesAsync();

                    if (velopackUpdateInfo == null)
                    {
                        _logger.Warning("[UPDATE] Không tìm thấy bản cập nhật");
                        return false;
                    }
                }

                var sizeMB = velopackUpdateInfo.TargetFullRelease.Size / 1024.0 / 1024.0;
                _logger.Information("[UPDATE] Tải xuống: {Package} (~{SizeMB:N1} MB)",
                    velopackUpdateInfo.TargetFullRelease.PackageId, sizeMB);

                // Download updates with progress callback
                await _updateManager.DownloadUpdatesAsync(velopackUpdateInfo, (percent) =>
                {
                    _logger.Debug("[UPDATE] Tiến trình: {Percent}%", percent);
                    progress?.Invoke(percent);
                });

                _logger.Information("[UPDATE] Tải hoàn tất - Đang cài đặt và khởi động lại...");

                // Apply updates and restart (this will terminate the current process)
                _updateManager.ApplyUpdatesAndRestart(velopackUpdateInfo);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[UPDATE] Lỗi khi tải/cài đặt: {Message}", ex.Message);
                return false;
            }
        }
    }
}
