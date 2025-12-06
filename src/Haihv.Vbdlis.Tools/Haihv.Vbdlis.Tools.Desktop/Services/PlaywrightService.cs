using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for managing Playwright browser instances with persistent context.
    /// This service maintains cookies, local storage, session storage, and other browser state
    /// across multiple page navigations and application sessions.
    /// </summary>
    public class PlaywrightService : IPlaywrightService, IDisposable
    {
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IBrowserContext? _context;
        private string? _userDataDirectory;
        private bool _disposed;

        public IBrowserContext? Context => _context;
        public IBrowser? Browser => _browser;
        public bool IsInitialized => _context != null && _browser != null;

        /// <summary>
        /// Initializes the Playwright browser with persistent context
        /// </summary>
        /// <param name="headless">Run browser in headless mode (default: false for better debugging)</param>
        /// <param name="userDataDir">Optional custom user data directory. If null, uses AppData/Playwright folder.</param>
        public async Task InitializeAsync(bool headless = false, string? userDataDir = null)
        {
            if (IsInitialized)
            {
                return; // Already initialized
            }

            try
            {
                // Set user data directory
                _userDataDirectory = userDataDir ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Haihv.Vbdlis.Tools",
                    "PlaywrightData"
                );

                // Ensure directory exists
                Directory.CreateDirectory(_userDataDirectory);

                // Install Playwright browsers if not already installed
                // Note: You need to run "playwright install" command once before first use
                // or call: Microsoft.Playwright.Program.Main(new[] { "install" });

                _playwright = await Playwright.CreateAsync();

                // Launch browser with persistent context
                // This preserves cookies, local storage, session storage, etc.
                _context = await _playwright.Chromium.LaunchPersistentContextAsync(
                    _userDataDirectory,
                    new BrowserTypeLaunchPersistentContextOptions
                    {
                        Headless = headless,

                        // Browser window settings
                        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },

                        // Accept downloads
                        AcceptDownloads = true,

                        // Enable JavaScript
                        JavaScriptEnabled = true,

                        // User agent (optional - uncomment to customize)
                        // UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",

                        // Locale and timezone
                        Locale = "vi-VN",
                        TimezoneId = "Asia/Ho_Chi_Minh",

                        // Permissions (optional)
                        // Permissions = new[] { "geolocation", "notifications" },

                        // Additional browser arguments (optional)
                        Args = new[]
                        {
                            "--disable-blink-features=AutomationControlled", // Hide automation
                            "--disable-dev-shm-usage",
                            "--no-sandbox"
                        },

                        // Ignore HTTPS errors (use with caution)
                        IgnoreHTTPSErrors = false,

                        // Slow down operations for debugging (milliseconds)
                        SlowMo = 0,
                    }
                );

                // Get browser instance from context
                _browser = _context.Browser;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to initialize Playwright. Make sure Playwright browsers are installed. " +
                    "Run: playwright install chromium", ex);
            }
        }

        /// <summary>
        /// Creates a new page in the persistent context
        /// </summary>
        public async Task<IPage> NewPageAsync()
        {
            if (!IsInitialized)
            {
                await InitializeAsync();
            }

            if (_context == null)
            {
                throw new InvalidOperationException("Browser context is not initialized");
            }

            var page = await _context.NewPageAsync();

            // Optional: Set default timeout for all operations
            page.SetDefaultTimeout(30000); // 30 seconds
            page.SetDefaultNavigationTimeout(60000); // 60 seconds for navigation

            return page;
        }

        /// <summary>
        /// Closes all pages and the browser context
        /// </summary>
        public async Task CloseAsync()
        {
            if (_context != null && _context.Pages.Count > 0)
            {
                // Create a copy of the pages collection to avoid modification during enumeration
                var pages = _context.Pages.ToList();
                
                // Close all pages
                foreach (var page in pages)
                {
                    await page.CloseAsync();
                }

                // Close context (this also saves all state to disk)
                await _context.CloseAsync();
                _context = null;
            }

            _browser = null;

            if (_playwright != null)
            {
                _playwright.Dispose();
                _playwright = null;
            }
        }

        /// <summary>
        /// Clears all browser data (cookies, local storage, etc.)
        /// </summary>
        public async Task ClearBrowserDataAsync()
        {
            if (IsInitialized)
            {
                await CloseAsync();
            }

            // Delete user data directory
            if (!string.IsNullOrEmpty(_userDataDirectory) && Directory.Exists(_userDataDirectory))
            {
                try
                {
                    Directory.Delete(_userDataDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Failed to clear browser data: {ex.Message}", ex);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Close browser synchronously (best effort)
                    try
                    {
                        CloseAsync().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Ignore errors during disposal
                    }
                }

                _disposed = true;
            }
        }
    }
}
