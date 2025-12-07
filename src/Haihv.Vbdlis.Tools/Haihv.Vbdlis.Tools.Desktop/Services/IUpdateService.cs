using System;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service interface for checking and downloading application updates
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Checks if a new version is available
        /// </summary>
        /// <returns>Update info if available, null otherwise</returns>
        Task<UpdateInfo?> CheckForUpdatesAsync();

        /// <summary>
        /// Downloads and installs the update
        /// </summary>
        /// <param name="updateInfo">Update information</param>
        /// <param name="progress">Progress callback (0-100)</param>
        Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, Action<int>? progress = null);

        /// <summary>
        /// Gets the current application version
        /// </summary>
        string CurrentVersion { get; }
    }

    /// <summary>
    /// Information about available update
    /// </summary>
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public long FileSize { get; set; }
        public bool IsRequired { get; set; }
    }
}
