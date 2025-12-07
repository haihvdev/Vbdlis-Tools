using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;
using Haihv.Vbdlis.Tools.Desktop.Views;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        public static IServiceProvider? Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                // Initialize MainWindow asynchronously after ensuring Playwright is ready
                _ = InitializeMainWindowAsync(desktop);

                // Handle application exit to cleanup resources
                desktop.ShutdownRequested += OnShutdownRequested;
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Initializes the main window after ensuring Playwright browsers are installed.
        /// This ensures the application is fully ready before showing the UI.
        /// </summary>
        private async Task InitializeMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // First, ensure Playwright browsers are installed
            await EnsurePlaywrightBrowsersAsync();

            // Then create and show the main window on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_serviceProvider == null)
                {
                    Log.Error("Service provider is null, cannot initialize main window");
                    return;
                }

                // Get MainWindowViewModel from DI container
                var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                    MinWidth = 1100,
                    MinHeight = 880,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen
                };

                // Show the main window
                desktop.MainWindow.Show();

                Log.Information("Main window initialized and shown");
            });
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Register Playwright installer service
            services.AddSingleton<IPlaywrightInstallerService, PlaywrightInstallerService>();

            // Register Playwright service as singleton to maintain browser context
            services.AddSingleton<IPlaywrightService, PlaywrightService>();

            // Register Credential service
            services.AddSingleton<ICredentialService, CredentialService>();

            // Register ViewModels
            services.AddSingleton<MainWindowViewModel>();

            // Add other services here as needed
        }

        /// <summary>
        /// Ensures Playwright browsers are installed on first run.
        /// This runs asynchronously in the background during app startup.
        /// Shows a UI window with installation progress.
        /// Supported on Windows and MacOS only.
        /// If installation fails, user can choose to retry or exit the application.
        /// </summary>
        private async Task EnsurePlaywrightBrowsersAsync()
        {
            if (_serviceProvider == null)
            {
                Log.Warning("Service provider is not initialized");
                return;
            }

            var installer = _serviceProvider.GetService<IPlaywrightInstallerService>();
            if (installer == null)
            {
                Log.Warning("PlaywrightInstallerService is not registered");
                return;
            }

            var os = installer.GetOperatingSystem();
            Log.Information("Checking Playwright browsers installation on {OS}...", os);

            // Check if auto-install is supported
            if (!installer.IsAutoInstallSupported())
            {
                Log.Warning("Auto-install not supported on {OS}. User must install Playwright manually.", os);
                return;
            }

            // Check if browsers are already installed
            if (installer.IsBrowsersInstalled())
            {
                Log.Information("Playwright browsers already installed");
                return;
            }

            // Try to install with retry capability
            var shouldRetry = true;
            while (shouldRetry)
            {
                var result = await TryInstallPlaywrightAsync(installer, os);

                switch (result)
                {
                    case InstallResult.Success:
                        Log.Information("Playwright installation completed successfully");
                        return;

                    case InstallResult.Retry:
                        Log.Information("User requested retry for Playwright installation");
                        shouldRetry = true;
                        break;

                    case InstallResult.Exit:
                        Log.Warning("User chose to exit application due to Playwright installation failure");
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                            {
                                desktop.Shutdown(1); // Exit code 1 indicates error
                            }
                        });
                        return;

                    default:
                        shouldRetry = false;
                        break;
                }
            }
        }

        /// <summary>
        /// Result of Playwright installation attempt
        /// </summary>
        private enum InstallResult
        {
            Success,
            Retry,
            Exit
        }

        /// <summary>
        /// Attempts to install Playwright browsers and returns the result
        /// </summary>
        private async Task<InstallResult> TryInstallPlaywrightAsync(IPlaywrightInstallerService installer, string os)
        {
            PlaywrightInstallationWindow? progressWindow = null;
            var tcs = new TaskCompletionSource<InstallResult>();

            try
            {
                // Create and show progress window on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    progressWindow = new PlaywrightInstallationWindow
                    {
                        DataContext = new PlaywrightInstallationViewModel
                        {
                            OperatingSystem = os
                        }
                    };

                    // Subscribe to window events
                    progressWindow.RetryRequested += (s, e) =>
                    {
                        progressWindow?.Close();
                        tcs.TrySetResult(InstallResult.Retry);
                    };

                    progressWindow.ExitRequested += (s, e) =>
                    {
                        progressWindow?.Close();
                        tcs.TrySetResult(InstallResult.Exit);
                    };

                    progressWindow.Show();
                });

                Log.Information("Playwright browsers not found. Starting installation...");
                progressWindow?.StartInstallation();

                // Install browsers with progress updates
                var success = await installer.EnsureBrowsersInstalledAsync(message =>
                {
                    Log.Information("[Playwright] {Message}", message);
                    progressWindow?.UpdateStatus(message);
                });

                if (success)
                {
                    Log.Information("Playwright browsers installed successfully");
                    progressWindow?.CompleteInstallation();

                    // Auto-close window after 3 seconds
                    if (progressWindow != null)
                    {
                        await progressWindow.AutoCloseAfterDelayAsync(3000);
                    }

                    return InstallResult.Success;
                }
                else
                {
                    Log.Error("Failed to install Playwright browsers");
                    progressWindow?.SetError("Không thể cài đặt Playwright browsers. Vui lòng kiểm tra kết nối mạng.");

                    // Wait for user action (retry or exit)
                    return await tcs.Task;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Playwright browsers installation");
                progressWindow?.SetError($"Lỗi: {ex.Message}");

                // Wait for user action (retry or exit)
                return await tcs.Task;
            }
        }

        private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            // Cleanup Playwright service
            if (_serviceProvider != null)
            {
                var playwrightService = _serviceProvider.GetService<IPlaywrightService>();
                if (playwrightService != null)
                {
                    await playwrightService.CloseAsync();
                }

                await _serviceProvider.DisposeAsync();
            }
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}