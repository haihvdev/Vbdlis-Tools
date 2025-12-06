using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Haihv.Vbdlis.Tools.Desktop.Models;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IPlaywrightService _playwrightService;
        private readonly ICredentialService _credentialService;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _loggedInUsername = string.Empty;

        [ObservableProperty]
        private string _loggedInServer = string.Empty;

        [ObservableProperty]
        private LoginViewModel _loginViewModel;

        public MainWindowViewModel(IPlaywrightService playwrightService, ICredentialService credentialService)
        {
            _playwrightService = playwrightService;
            _credentialService = credentialService;

            // Initialize LoginViewModel with PlaywrightService
            _loginViewModel = new LoginViewModel(playwrightService)
            {
                Server = "https://bgi.mplis.gov.vn/dc/"
            };

            // Subscribe to login events
            _loginViewModel.LoginSuccessful += OnLoginSuccessful;
            _loginViewModel.LoginCancelled += OnLoginCancelled;

            // Start with not logged in
            IsLoggedIn = false;

            // Try to load saved credentials
            _ = LoadSavedCredentialsAsync();
        }

        private async Task LoadSavedCredentialsAsync()
        {
            var credentials = await _credentialService.LoadCredentialsAsync();
            if (credentials != null)
            {
                LoginViewModel.Server = credentials.Server;
                LoginViewModel.Username = credentials.Username;
                LoginViewModel.Password = credentials.Password;
                LoginViewModel.HeadlessBrowser = credentials.HeadlessBrowser;
                LoginViewModel.RememberMe = true; // User had saved credentials, so check RememberMe
            }
        }

        private async void OnLoginSuccessful(object? sender, LoginEventArgs e)
        {
            // Update login state
            IsLoggedIn = true;
            LoggedInUsername = e.Username;
            LoggedInServer = e.Server;

            // Save credentials for next time only if RememberMe is checked
            if (e.RememberMe)
            {
                await _credentialService.SaveCredentialsAsync(new LoginSessionInfo(e.Server, e.Username, e.Password, e.HeadlessBrowser));
            }
            else
            {
                // Clear saved credentials if RememberMe is unchecked
                await _credentialService.ClearCredentialsAsync();
            }
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

        [RelayCommand]
        private async Task LogoutAsync()
        {
            // Clear login state
            IsLoggedIn = false;
            LoggedInUsername = string.Empty;
            LoggedInServer = string.Empty;

            // Clear saved credentials
            await _credentialService.ClearCredentialsAsync();

            // Close browser and clear session
            await _playwrightService.CloseAsync();

            // Reset login form with default server
            LoginViewModel.Server = "https://bgi.mplis.gov.vn/dc/";
            LoginViewModel.Username = string.Empty;
            LoginViewModel.Password = string.Empty;
            LoginViewModel.ErrorMessage = string.Empty;
        }
    }
}
