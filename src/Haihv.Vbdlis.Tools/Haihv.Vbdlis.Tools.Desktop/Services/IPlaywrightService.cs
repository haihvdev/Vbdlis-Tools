using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service interface for managing Playwright browser instances with persistent context
    /// </summary>
    public interface IPlaywrightService
    {
        /// <summary>
        /// Initializes the Playwright browser with persistent context
        /// </summary>
        /// <param name="headless">Run browser in headless mode</param>
        /// <param name="userDataDir">Optional custom user data directory path. If null, uses default.</param>
        Task InitializeAsync(bool headless = false, string? userDataDir = null);

        /// <summary>
        /// Gets the current browser context (with cookies, local storage, etc.)
        /// </summary>
        IBrowserContext? Context { get; }

        /// <summary>
        /// Gets the current browser instance
        /// </summary>
        IBrowser? Browser { get; }

        /// <summary>
        /// Creates a new page in the current context
        /// </summary>
        Task<IPage> NewPageAsync();

        /// <summary>
        /// Closes all pages and disposes the browser
        /// </summary>
        Task CloseAsync();

        /// <summary>
        /// Clears all browser data (cookies, local storage, session storage, etc.)
        /// </summary>
        Task ClearBrowserDataAsync();

        /// <summary>
        /// Checks if the browser is initialized
        /// </summary>
        bool IsInitialized { get; }
    }
}
