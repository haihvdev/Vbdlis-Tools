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
            _logger.Information("========== BẮT ĐẦU KIỂM TRA CẬP NHẬT ==========");
            _logger.Information("Phiên bản hiện tại: {CurrentVersion}", CurrentVersion);

            if (_updateManager == null)
            {
                _logger.Warning("UpdateManager chưa được khởi tạo. Không thể kiểm tra cập nhật.");
                _logger.Information("========== KẾT THÚC KIỂM TRA CẬP NHẬT (FAILED) ==========");
                return null;
            }

            try
            {
                // Log installation status for debugging
                var isInstalled = _updateManager.IsInstalled;
                _logger.Information("Trạng thái cài đặt Velopack: IsInstalled={IsInstalled}", isInstalled);

                if (!isInstalled)
                {
                    _logger.Warning("Ứng dụng đang chạy ở chế độ portable/development.");
                    _logger.Warning("Tính năng tự động cập nhật chỉ hoạt động khi cài đặt qua Velopack installer.");
                    _logger.Information("========== KẾT THÚC KIỂM TRA CẬP NHẬT (PORTABLE MODE) ==========");
                    return null;
                }

                _logger.Information("Đang kết nối tới GitHub để kiểm tra phiên bản mới...");
                _logger.Information("GitHub repo: https://github.com/{Owner}/{Repo}", GitHubRepoOwner, GitHubRepoName);

                var updateInfo = await _updateManager.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    _logger.Information("Không có phiên bản mới. Đã sử dụng phiên bản mới nhất: {Version}", CurrentVersion);
                    _logger.Information("========== KẾT THÚC KIỂM TRA CẬP NHẬT (NO UPDATE) ==========");
                    return null;
                }

                var newVersion = updateInfo.TargetFullRelease.Version.ToString();

                _logger.Information("🎉 TÌM THẤY PHIÊN BẢN MỚI!");
                _logger.Information("   Phiên bản hiện tại: {CurrentVersion}", CurrentVersion);
                _logger.Information("   Phiên bản mới: {NewVersion}", newVersion);
                _logger.Information("   Kích thước file: {Size:N0} bytes (~{SizeMB:N1} MB)",
                    updateInfo.TargetFullRelease.Size,
                    updateInfo.TargetFullRelease.Size / 1024.0 / 1024.0);
                _logger.Information("========== KẾT THÚC KIỂM TRA CẬP NHẬT (UPDATE FOUND) ==========");

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
                _logger.Error(ex, "LỖI khi kiểm tra cập nhật");
                _logger.Error("Chi tiết lỗi: {Message}", ex.Message);
                _logger.Error("Stack trace: {StackTrace}", ex.StackTrace);
                _logger.Information("========== KẾT THÚC KIỂM TRA CẬP NHẬT (ERROR) ==========");
                return null;
            }
        }


        /// <summary>
        /// Downloads and installs the update using Velopack
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, Action<int>? progress = null)
        {
            _logger.Information("========== BẮT ĐẦU TẢI VÀ CÀI ĐẶT CẬP NHẬT ==========");
            _logger.Information("Phiên bản đích: {Version}", updateInfo.Version);

            if (_updateManager == null)
            {
                _logger.Warning("UpdateManager chưa được khởi tạo. Không thể tải cập nhật.");
                _logger.Information("========== KẾT THÚC TẢI CẬP NHẬT (FAILED) ==========");
                return false;
            }

            try
            {
                _logger.Information("Đang kiểm tra lại thông tin cập nhật từ Velopack...");

                // Check for updates again to get the UpdateInfo object from Velopack
                var velopackUpdateInfo = await _updateManager.CheckForUpdatesAsync();

                if (velopackUpdateInfo == null)
                {
                    _logger.Warning("Không tìm thấy bản cập nhật khi kiểm tra lại");
                    _logger.Information("========== KẾT THÚC TẢI CẬP NHẬT (NO UPDATE) ==========");
                    return false;
                }

                _logger.Information("Xác nhận có bản cập nhật. Bắt đầu tải xuống...");
                _logger.Information("   Package: {Package}", velopackUpdateInfo.TargetFullRelease.PackageId);
                _logger.Information("   Kích thước: {Size:N0} bytes", velopackUpdateInfo.TargetFullRelease.Size);

                // Download updates with progress callback
                await _updateManager.DownloadUpdatesAsync(velopackUpdateInfo, (percent) =>
                {
                    _logger.Information("Tiến trình tải: {Percent}%", percent);
                    progress?.Invoke(percent);
                });

                _logger.Information("✅ Tải xuống hoàn tất!");
                _logger.Information("Đang áp dụng bản cập nhật và khởi động lại ứng dụng...");

                // Apply updates and restart (this will terminate the current process)
                _updateManager.ApplyUpdatesAndRestart(velopackUpdateInfo);

                _logger.Information("========== ỨNG DỤNG SẼ KHỞI ĐỘNG LẠI ==========");

                // This line won't be reached as the app will restart
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "LỖI khi tải/cài đặt cập nhật");
                _logger.Error("Chi tiết lỗi: {Message}", ex.Message);
                _logger.Error("Stack trace: {StackTrace}", ex.StackTrace);
                _logger.Information("========== KẾT THÚC TẢI CẬP NHẬT (ERROR) ==========");
                return false;
            }
        }
    }
}
