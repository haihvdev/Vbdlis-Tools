using System;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service interface for detecting Playwright browsers
    /// </summary>
    public interface IPlaywrightInstallerService
    {
        /// <summary>
        /// Checks if Playwright browsers are installed (bundled or in system cache)
        /// </summary>
        /// <returns>True if browsers are found, false otherwise</returns>
        bool IsBrowsersInstalled();

        /// <summary>
        /// Gets the current operating system
        /// </summary>
        string GetOperatingSystem();

        /// <summary>
        /// Ensures Playwright browsers are available; installs if missing.
        /// </summary>
        Task<bool> EnsureBrowsersInstalledAsync(Action<string>? onStatusChange = null, bool showTerminalWindow = false,
            bool forceInstall = false);
    }
}