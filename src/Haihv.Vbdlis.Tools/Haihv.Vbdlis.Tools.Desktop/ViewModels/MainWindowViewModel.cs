using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.Services.Vbdlis;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IPlaywrightService _playwrightService;
        private readonly ICredentialService _credentialService;
        private LoginSessionInfo? _currentLoginSession;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _loggedInUsername = string.Empty;

        [ObservableProperty]
        private string _loggedInServer = string.Empty;

        [ObservableProperty]
        private LoginViewModel _loginViewModel;

        [ObservableProperty]
        private ViewModelBase? _currentViewModel;

        [ObservableProperty]
        private CungCapThongTinViewModel? _cungCapThongTinViewModel;

        [ObservableProperty]
        private DemoViewModel? _demoViewModel;

        [ObservableProperty]
        private HomeViewModel? _homeViewModel;

        public static string AppVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}" : "1.0.0";
            }
        }

        public static string AppVersionText => $"Phiên bản: {AppVersion}";

        public static string CopyrightText => $"© {DateTime.Now.Year} vpdkbacninh.vn | haihv.vn";

        public MainWindowViewModel(IPlaywrightService playwrightService, ICredentialService credentialService)
        {
            _playwrightService = playwrightService;
            _credentialService = credentialService;

            // Initialize LoginViewModel with PlaywrightService
            _loginViewModel = new LoginViewModel(playwrightService)
            {
                Server = "https://bgi.mplis.gov.vn/dc/",
                HeadlessBrowser = true
            };

            // Subscribe to login events
            _loginViewModel.LoginSuccessful += OnLoginSuccessful;
            _loginViewModel.LoginCancelled += OnLoginCancelled;
            _playwrightService.SessionExpired += OnSessionExpired;

            // Start with not logged in
            IsLoggedIn = false;

            // Create Home ViewModel
            _homeViewModel = new HomeViewModel(this);
            _currentViewModel = _homeViewModel;

            // Try to load saved credentials
            _ = LoadSavedCredentialsAsync(autoLogin: true);
        }

        private async Task LoadSavedCredentialsAsync(bool autoLogin)
        {
            var credentials = await _credentialService.LoadCredentialsAsync();
            if (credentials != null)
            {
                LoginViewModel.Server = credentials.Server;
                LoginViewModel.Username = credentials.Username;
                LoginViewModel.Password = credentials.Password;
                LoginViewModel.HeadlessBrowser = credentials.HeadlessBrowser;
                LoginViewModel.RememberMe = true; // User had saved credentials, so check RememberMe
                if (autoLogin)
                {
                    // Nếu có thông tin đăng nhập đã lưu, tự động đăng nhập
                    await LoginViewModel.LoginAsync();
                }
            }
        }

        private async void OnLoginSuccessful(object? sender, LoginEventArgs e)
        {
            // Update login state
            IsLoggedIn = true;
            LoggedInUsername = e.Username;
            LoggedInServer = e.Server;

            // Store current login session
            _currentLoginSession = new LoginSessionInfo(e.Server, e.Username, e.Password, e.HeadlessBrowser);

            // Save credentials for next time only if RememberMe is checked
            if (e.RememberMe)
            {
                await _credentialService.SaveCredentialsAsync(_currentLoginSession);
            }
            else
            {
                // Clear saved credentials if RememberMe is unchecked
                await _credentialService.ClearCredentialsAsync();
            }

            UpdateHomeViewModel();
        }

        private void OnLoginCancelled(object? sender, EventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        }

        private void OnSessionExpired(string message)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await HandleSessionExpiredAsync(message);
            });
        }

        private async Task HandleSessionExpiredAsync(string message)
        {
            if (!IsLoggedIn)
            {
                return;
            }

            await LogoutAsync(clearSavedCredentials: false);
            LoginViewModel.ErrorMessage = message;
            CurrentViewModel = HomeViewModel;
            await LoadSavedCredentialsAsync(autoLogin: false);
        }

        [RelayCommand]
        private void ShowCungCapThongTin()
        {
            // Create ViewModel if not already created
            if (CungCapThongTinViewModel == null && _currentLoginSession != null)
            {
                var searchService = new CungCapThongTinGiayChungNhanService(_playwrightService, _currentLoginSession);
                CungCapThongTinViewModel = new CungCapThongTinViewModel(searchService);
            }

            CurrentViewModel = CungCapThongTinViewModel;
        }

        [RelayCommand]
        private void ShowHome()
        {
            // Update HomeViewModel data before switching
            UpdateHomeViewModel();

            CurrentViewModel = HomeViewModel;
        }

        // [RelayCommand]
        // private void ShowDemo()
        // {
        //     // Create ViewModel if not already created
        //     if (DemoViewModel == null)
        //     {
        //         DemoViewModel = new DemoViewModel();
        //     }

        //     CurrentViewModel = DemoViewModel;
        // }

        [RelayCommand]
        private async Task Logout()
        {
            await LogoutAsync(clearSavedCredentials: true);
        }

        private async Task LogoutAsync(bool clearSavedCredentials)
        {
            // Clear login state
            IsLoggedIn = false;
            LoggedInUsername = string.Empty;
            LoggedInServer = string.Empty;
            _currentLoginSession = null;
            CungCapThongTinViewModel = null;

            if (clearSavedCredentials)
            {
                // Clear saved credentials
                await _credentialService.ClearCredentialsAsync();
            }

            // Close browser and clear session
            await _playwrightService.CloseAsync();

            // Reset login form with default server
            LoginViewModel.Server = "https://bgi.mplis.gov.vn/dc/";
            LoginViewModel.Username = string.Empty;
            LoginViewModel.Password = string.Empty;
            LoginViewModel.ErrorMessage = string.Empty;
            UpdateHomeViewModel();
            CurrentViewModel = HomeViewModel;
        }

        private void UpdateHomeViewModel()
        {
            if (HomeViewModel != null)
            {
                HomeViewModel.LoggedInUsername = LoggedInUsername;
                HomeViewModel.LoggedInServer = LoggedInServer;
            }
        }
    }
}
