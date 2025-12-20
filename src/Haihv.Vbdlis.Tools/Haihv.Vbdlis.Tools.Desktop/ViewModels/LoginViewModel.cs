using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Microsoft.Playwright;
using System.Diagnostics;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels
{
    public partial class LoginViewModel(IPlaywrightService? playwrightService = null) : ViewModelBase
    {
        [ObservableProperty] private string _server = string.Empty;

        [ObservableProperty] private string _username = string.Empty;

        [ObservableProperty] private string _password = string.Empty;

        [ObservableProperty] private bool _isLoggingIn;

        [ObservableProperty] private string _errorMessage = string.Empty;

        [ObservableProperty] private bool _rememberMe = true; // Default to true for convenience

        [ObservableProperty] private bool _headlessBrowser; // Default to false (show browser)

        [ObservableProperty] private string _loginStatusMessage = string.Empty;
        private readonly ILogger _logger = Log.ForContext<LoginViewModel>();

        partial void OnIsLoggingInChanged(bool value)
        {
            if (!value)
            {
                LoginStatusMessage = string.Empty;
            }

            LoginCommand.NotifyCanExecuteChanged();
        }

        public event EventHandler<LoginEventArgs>? LoginSuccessful;
        public event EventHandler? LoginCancelled;

        [RelayCommand(CanExecute = nameof(CanLogin))]
        public async Task LoginAsync()
        {
            try
            {
                IsLoggingIn = true;
                ErrorMessage = string.Empty;
                _logger.Information("Attempting login to server {Server} with username {Username}", Server, Username);
                UpdateLoginStatus("Đang kiểm tra thông tin đã nhập...");

                // Validate inputs
                if (string.IsNullOrWhiteSpace(Server))
                {
                    _logger.Warning("Server address is empty.");
                    ErrorMessage = "Vui lòng nhập địa chỉ máy chủ";
                    return;
                }

                if (string.IsNullOrWhiteSpace(Username))
                {
                    _logger.Warning("Username is empty.");
                    ErrorMessage = "Vui lòng nhập tài khoản";
                    return;
                }

                if (string.IsNullOrWhiteSpace(Password))
                {
                    _logger.Warning("Password is empty.");
                    ErrorMessage = "Vui lòng nhập mật khẩu";
                    return;
                }

                // Use Playwright to login
                if (playwrightService != null)
                {
                    _logger.Debug("Using Playwright service for login automation.");
                    await PerformPlaywrightLoginAsync();
                }
                else
                {
                    // Fallback: Just raise success event without browser automation
                    _logger.Debug("Playwright service not available. Skipping browser automation.");
                    UpdateLoginStatus("Đăng nhập thành công.");
                    NotifyLoginSuccess();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Login failed with exception.");
                ErrorMessage = $"Đăng nhập thất bại: {ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private async Task PerformPlaywrightLoginAsync()
        {
            if (playwrightService == null) return;

            // Initialize Playwright browser (reuse existing context if available)
            _logger.Debug("Initializing Playwright browser. Headless mode: {Headless}", HeadlessBrowser);
            UpdateLoginStatus("Đang khởi tạo phiên trình duyệt...");
            await playwrightService.InitializeAsync(headless: HeadlessBrowser);

            // Get the default page (first page in context)
            var pages = playwrightService.Context?.Pages;

            // Always use the first (default) page
            var page = (pages == null || pages.Count == 0) ? await playwrightService.NewPageAsync() : pages[0];

            // Navigate to server
            UpdateLoginStatus("Đang kết nối đến máy chủ...");
            await page.GotoAsync(Server);

            // Wait for navigation and check if redirected to login page
            UpdateLoginStatus("Đang tải dữ liệu từ máy chủ...");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var currentUrl = page.Url;

            // Check if redirected to authentication page
            if (currentUrl.Contains("authen.mplis.gov.vn/account/login"))
            {
                // On login page - perform login
                _logger.Information("Redirected to login page. Performing login for user {Username}.", Username);
                await PerformLoginAsync(page);
            }
            else
            {
                // Not redirected to login page - check if already logged in with correct user
                try
                {
                    // Wait for user profile element to appear
                    UpdateLoginStatus("Đang kiểm tra trạng thái đăng nhập...");
                    await page.WaitForSelectorAsync("a.user-profile b",
                        new PageWaitForSelectorOptions { Timeout = 15000 });

                    // Get the logged-in username from the page
                    var loggedInUsername = await page.InnerTextAsync("a.user-profile b");

                    // Check if the logged-in user matches the requested username
                    if (loggedInUsername.Trim().Equals(Username.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        // Already logged in with correct user
                        UpdateLoginStatus("Đăng nhập thành công.");
                        NotifyLoginSuccess();
                    }
                    else
                    {
                        // Logged in with different user - need to logout first
                        try
                        {
                            // First, click on the user profile dropdown to reveal logout link
                            UpdateLoginStatus("Đang đăng xuất tài khoản hiện tại...");
                            await page.ClickAsync("a.user-profile");

                            // Wait a moment for dropdown to appear
                            await Task.Delay(500);

                            // Now click the logout link
                            await page.ClickAsync("a[href*='/Account/Logout']");
                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                            // After logout, should be redirected to login page
                            // Retry the login process
                            UpdateLoginStatus("Đang quay lại trang đăng nhập...");
                            await PerformLoginAsync(page);
                        }
                        catch (Exception logoutEx)
                        {
                            Debug.WriteLine($"Logout failed: {logoutEx.Message}");
                            ErrorMessage =
                                $"Không thể đăng xuất người dùng '{loggedInUsername}'. Vui lòng đăng xuất thủ công trong trình duyệt.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    // User profile element not found - might not be logged in
                    // Try to login anyway
                    ErrorMessage = "Không thể xác định trạng thái đăng nhập. Vui lòng thử lại.";
                }
            }
        }

        private async Task PerformLoginAsync(IPage page)
        {
            UpdateLoginStatus("Đang điền thông tin đăng nhập...");
            // Need to login - fill in login form
            await page.FillAsync("input[name='username']", Username);
            await page.FillAsync("input[name='password']", Password);

            // Click login button
            UpdateLoginStatus("Đang gửi thông tin đăng nhập...");
            await page.ClickAsync("button[type='submit'].login100-form-btn");

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            // Check if login was successful by verifying we're back on the main site
            var finalUrl = page.Url;

            if (!finalUrl.Contains("authen.mplis.gov.vn"))
            {
                // Wait for navigation after login
                // Wait for user profile element to appear
                await page.WaitForSelectorAsync("a.user-profile b", new PageWaitForSelectorOptions { Timeout = 15000 });
                // Login successful
                UpdateLoginStatus("Đăng nhập thành công.");
                NotifyLoginSuccess();
            }
            else
            {
                // Still on login page - login failed
                ErrorMessage = "Đăng nhập thất bại. Vui lòng kiểm tra lại tên tài khoản và mật khẩu.";
            }
        }

        private bool CanLogin()
        {
            return !IsLoggingIn &&
                   !string.IsNullOrWhiteSpace(Server) &&
                   !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        [RelayCommand]
        private void Cancel()
        {
            // Clear fields
            Server = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
            ErrorMessage = string.Empty;
            LoginStatusMessage = string.Empty;

            // Raise cancelled event
            LoginCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateLoginStatus(string message)
        {
            LoginStatusMessage = message;
        }

        private void NotifyLoginSuccess()
        {
            playwrightService?.CacheLoginInfo(Server, Username, Password, HeadlessBrowser);

            LoginSuccessful?.Invoke(this, new LoginEventArgs
            {
                Server = Server,
                Username = Username,
                Password = Password,
                RememberMe = RememberMe,
                HeadlessBrowser = HeadlessBrowser
            });
        }

        partial void OnServerChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }

        partial void OnUsernameChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }

        partial void OnPasswordChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    public class LoginEventArgs : EventArgs
    {
        public string Server { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
        public bool HeadlessBrowser { get; set; }
    }
}
