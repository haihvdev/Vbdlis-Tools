using System;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Microsoft.Playwright;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service interface for managing Playwright browser instances with persistent context
    /// </summary>
    public interface IPlaywrightService
    {
        /// <summary>
        /// Notifies UI about long-running Playwright status (e.g., auto re-login).
        /// </summary>
        event Action<string>? StatusChanged;

        /// <summary>
        /// Raised when the session is expired and cannot be restored automatically.
        /// </summary>
        event Action<string>? SessionExpired;

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
        /// Caches the most recent successful login credentials in memory.
        /// </summary>
        void CacheLoginInfo(string server, string username, string password, bool headlessBrowser);

        /// <summary>
        /// Gets the cached login credentials, if any.
        /// </summary>
        LoginSessionInfo? CachedLoginInfo { get; }

        /// <summary>
        /// Checks if the browser is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Ensures a page is created and navigated to the specified URL
        /// </summary>
        /// <param name="page">
        /// An existing page instance, or null to create a new one
        /// </param>
        /// <param name="url">
        /// The URL to navigate the page to
        /// </param>
        /// <returns></returns>
        Task<IPage?> EnsurePageAsync(IPage? page, string url);
    }
}
