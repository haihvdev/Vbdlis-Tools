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

            // Get current version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

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
            if (_updateManager == null)
            {
                _logger.Warning("UpdateManager not initialized. Cannot check for updates.");
                return null;
            }

            try
            {
                _logger.Information("Checking for updates using Velopack...");

                var updateInfo = await _updateManager.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    _logger.Information("Already on latest version: {Version}", CurrentVersion);
                    return null;
                }

                var newVersion = updateInfo.TargetFullRelease.Version.ToString();

                _logger.Information("Update available: {CurrentVersion} -> {NewVersion}",
                    CurrentVersion, newVersion);

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
                _logger.Error(ex, "Error checking for updates");
                return null;
            }
        }


        /// <summary>
        /// Downloads and installs the update using Velopack
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, Action<int>? progress = null)
        {
            if (_updateManager == null)
            {
                _logger.Warning("UpdateManager not initialized. Cannot download updates.");
                return false;
            }

            try
            {
                _logger.Information("Downloading update to version: {Version}", updateInfo.Version);

                // Check for updates again to get the UpdateInfo object from Velopack
                var velopackUpdateInfo = await _updateManager.CheckForUpdatesAsync();

                if (velopackUpdateInfo == null)
                {
                    _logger.Warning("No update available");
                    return false;
                }

                // Download updates with progress callback
                await _updateManager.DownloadUpdatesAsync(velopackUpdateInfo, (percent) =>
                {
                    progress?.Invoke(percent);
                });

                _logger.Information("Update downloaded successfully. Applying update and restarting...");

                // Apply updates and restart (this will terminate the current process)
                _updateManager.ApplyUpdatesAndRestart(velopackUpdateInfo);

                // This line won't be reached as the app will restart
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error downloading/installing update");
                return false;
            }
        }
    }
}
