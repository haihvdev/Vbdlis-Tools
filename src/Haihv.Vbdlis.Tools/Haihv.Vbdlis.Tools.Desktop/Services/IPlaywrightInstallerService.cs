using System;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service interface for managing Playwright browser installation
    /// </summary>
    public interface IPlaywrightInstallerService
    {
        /// <summary>
        /// Checks if Playwright browsers are installed
        /// </summary>
        /// <returns>True if browsers are installed, false otherwise</returns>
        bool IsBrowsersInstalled();

        /// <summary>
        /// Installs Playwright browsers asynchronously
        /// </summary>
        /// <param name="progress">Optional callback to report installation progress</param>
        /// <returns>True if installation succeeded, false otherwise</returns>
        Task<bool> InstallBrowsersAsync(Action<string>? progress = null);

        /// <summary>
        /// Ensures Playwright browsers are installed. If not, installs them automatically.
        /// Only works on Windows and MacOS. Linux is not supported yet.
        /// </summary>
        /// <param name="progress">Optional callback to report progress</param>
        /// <returns>True if browsers are ready to use, false otherwise</returns>
        Task<bool> EnsureBrowsersInstalledAsync(Action<string>? progress = null);

        /// <summary>
        /// Gets the current operating system
        /// </summary>
        string GetOperatingSystem();

        /// <summary>
        /// Checks if auto-installation is supported on the current OS
        /// </summary>
        bool IsAutoInstallSupported();
    }
}
