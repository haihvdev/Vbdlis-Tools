using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Extensions;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Microsoft.Playwright;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for managing Playwright browser instances with persistent context.
    /// This service maintains cookies, local storage, session storage, and other browser state
    /// across multiple page navigations and application sessions.
    /// </summary>
    public sealed class PlaywrightService : IPlaywrightService, IDisposable
    {
        private const int DefaultNavigationTimeoutMs = 60000;
        private const int ReLoginMaxAttempts = 1;
        private const string LoginUsernameSelector = "input[name='username']";
        private const string LoginPasswordSelector = "input[name='password']";
        private const string LoginSubmitSelector = "button[type='submit'].login100-form-btn";

        private IPlaywright? _playwright;
        private string? _userDataDirectory;
        private bool _disposed;
        private readonly ILogger _logger = Log.ForContext<PlaywrightService>();
        private readonly SemaphoreSlim _navigationSemaphore = new(1, 1);

        public event Action<string>? StatusChanged;
        public event Action<string>? SessionExpired;

        public IBrowserContext? Context { get; private set; }

        public IBrowser? Browser { get; private set; }

        public bool IsInitialized => Context != null && Browser != null;
        public async Task<IPage?> EnsurePageAsync(IPage? page, string url)
        {
            _logger.Debug("EnsurePageAsync - IsInitialized: {IsInit}, Page null: {PageNull}",
                IsInitialized, page == null);

            if (!IsInitialized)
            {
                _logger.Debug("Initializing playwright service...");
                await InitializeAsync();
            }

            // Tạo page mới nếu chưa có
            if (page == null)
            {
                _logger.Debug("Creating new page...");
                page = await NewPageAsync();
                _logger.Debug("New page created");
            }

            await _navigationSemaphore.WaitAsync();
            try
            {
                for (var attempt = 0; attempt <= ReLoginMaxAttempts; attempt++)
                {
                    // LUÔN reload trang để tránh cache và tự động re-login nếu timeout
                    _logger.Debug("Navigating to: {Url} (attempt {Attempt}/{MaxAttempt})",
                        url, attempt + 1, ReLoginMaxAttempts + 1);

                    NotifyStatus("Đang tải trang, vui lòng chờ...");
                    try
                    {
                        await page.GotoAsync(url, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = DefaultNavigationTimeoutMs
                        });
                    }
                    catch (TimeoutException ex)
                    {
                        _logger.Warning(ex, "Navigation timeout for {Url} (attempt {Attempt}/{MaxAttempt})",
                            url, attempt + 1, ReLoginMaxAttempts + 1);
                        NotifyStatus("Tải trang đang chậm, đang thử lại...");
                        if (attempt == ReLoginMaxAttempts)
                        {
                            throw;
                        }

                        continue;
                    }

                    _logger.Debug("Page loaded, waiting for page to be ready...");

                    // Wait a bit for scripts to load
                    await Task.Delay(1000);

                    // Kiểm tra xem có cần login lại không
                    if (!IsAuthenticationUrl(page.Url))
                    {
                        _logger.Debug("Page ready at: {Url}", page.Url);
                        break;
                    }

                    NotifyStatus("Phiên đăng nhập đã hết hạn, đang tự động đăng nhập lại...");
                    _logger.Warning("Session expired (redirected to auth). Attempting re-login...");

                    var reLogin = await TryReLoginAsync(page);
                    if (!reLogin)
                    {
                        var message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                        NotifyStatus(message);
                        NotifySessionExpired(message);
                        throw new InvalidOperationException(
                            "Phiên đăng nhập đã hết hạn và không thể tự động đăng nhập lại. Vui lòng đăng nhập lại.");
                    }

                    if (attempt == ReLoginMaxAttempts)
                    {
                        throw new InvalidOperationException(
                            "Không thể điều hướng tới trang đích sau khi đã tự động đăng nhập lại. Vui lòng thử lại.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to navigate to CungCapThongTin page");
                throw;
            }
            finally
            {
                _navigationSemaphore.Release();
            }

            return page;
        }

        public LoginSessionInfo? CachedLoginInfo { get; private set; }

        private static bool IsAuthenticationUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Contains(UrlExtensions.AuthenBaseUrl, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> TryReLoginAsync(IPage page)
        {
            var loginInfo = CachedLoginInfo;
            if (loginInfo == null ||
                string.IsNullOrWhiteSpace(loginInfo.Username) ||
                string.IsNullOrWhiteSpace(loginInfo.Password))
            {
                NotifyStatus("Không có thông tin đăng nhập lưu, cần đăng nhập lại.");
                _logger.Warning("No cached credentials available; cannot auto re-login.");
                return false;
            }

            try
            {
                if (!IsAuthenticationUrl(page.Url))
                {
                    return true;
                }

                NotifyStatus("Đang mở form đăng nhập...");
                // Ensure we're on the login form
                await page.WaitForSelectorAsync(LoginUsernameSelector, new PageWaitForSelectorOptions
                {
                    Timeout = 15000
                });

                await page.FillAsync(LoginUsernameSelector, loginInfo.Username);
                await page.FillAsync(LoginPasswordSelector, loginInfo.Password);

                try
                {
                    await page.ClickAsync(LoginSubmitSelector);
                }
                catch
                {
                    await page.ClickAsync("button[type='submit']");
                }

                NotifyStatus("Đang đăng nhập lại...");
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded,
                        new PageWaitForLoadStateOptions { Timeout = 15000 });
                }
                catch
                {
                    // Best effort
                }

                // Wait until we leave the auth domain (poll to avoid relying on overload availability)
                var timeoutAt = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < timeoutAt)
                {
                    if (!IsAuthenticationUrl(page.Url))
                    {
                        NotifyStatus("Đăng nhập lại thành công, đang tiếp tục...");
                        _logger.Debug("Re-login succeeded. Current url: {Url}", page.Url);
                        return true;
                    }

                    await Task.Delay(250);
                }

                _logger.Warning("Re-login did not leave auth domain. Current url: {Url}", page.Url);
                NotifyStatus("Đăng nhập lại không thành công. Vui lòng đăng nhập lại.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Auto re-login failed");
                NotifyStatus("Đăng nhập lại gặp lỗi. Vui lòng thử lại.");
                return false;
            }
        }

        private void NotifyStatus(string message)
        {
            StatusChanged?.Invoke(message);
        }

        private void NotifySessionExpired(string message)
        {
            SessionExpired?.Invoke(message);
        }

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
                Context = await _playwright.Chromium.LaunchPersistentContextAsync(
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
                        Args =
                        [
                            "--disable-blink-features=AutomationControlled", // Hide automation
                            "--disable-dev-shm-usage",
                            "--no-sandbox"
                        ],

                        // Ignore HTTPS errors (use with caution)
                        IgnoreHTTPSErrors = false,

                        // Slow down operations for debugging (milliseconds)
                        SlowMo = 0,
                    }
                );

                // Get browser instance from context
                Browser = Context.Browser;
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

            if (Context == null)
            {
                throw new InvalidOperationException("Browser context is not initialized");
            }

            var page = await Context.NewPageAsync();

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
            if (Context is { Pages.Count: > 0 })
            {
                // Create a copy of the pages collection to avoid modification during enumeration
                var pages = Context.Pages.ToList();

                // Close all pages
                foreach (var page in pages)
                {
                    await page.CloseAsync();
                }

                // Close context (this also saves all state to disk)
                await Context.CloseAsync();
                Context = null;
            }

            Browser = null;

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

        public void CacheLoginInfo(string server, string username, string password, bool headlessBrowser)
        {
            CachedLoginInfo = new LoginSessionInfo(server, username, password, headlessBrowser);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
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
