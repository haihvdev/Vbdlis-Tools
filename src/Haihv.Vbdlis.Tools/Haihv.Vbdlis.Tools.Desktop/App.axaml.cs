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

            // Check for updates after MainWindow is shown (non-blocking)
            _ = CheckForUpdatesAsync();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Register Playwright installer service
            services.AddSingleton<IPlaywrightInstallerService, PlaywrightInstallerService>();

            // Register Playwright service as singleton to maintain browser context
            services.AddSingleton<IPlaywrightService, PlaywrightService>();

            // Register Credential service
            services.AddSingleton<ICredentialService, CredentialService>();

            // Register Update service
            services.AddSingleton<IUpdateService, UpdateService>();

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

        /// <summary>
        /// Checks for application updates in the background
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Wait a bit after app startup before checking
                await Task.Delay(TimeSpan.FromSeconds(5));

                if (_serviceProvider == null)
                    return;

                var updateService = _serviceProvider.GetService<IUpdateService>();
                if (updateService == null)
                    return;

                Log.Information("Checking for updates...");
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo != null)
                {
                    Log.Information("Update available: {Version}", updateInfo.Version);

                    // Show update notification on UI thread
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var result = await ShowUpdateDialogAsync(updateInfo);

                        if (result)
                        {
                            // User wants to update
                            Log.Information("User accepted update");
                            await updateService.DownloadAndInstallUpdateAsync(updateInfo, progress =>
                            {
                                Log.Information("Download progress: {Progress}%", progress);
                            });

                            // Installer will launch and close this app
                        }
                    });
                }
                else
                {
                    Log.Information("No updates available");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for updates");
            }
        }

        /// <summary>
        /// Shows update dialog to user
        /// </summary>
        private async Task<bool> ShowUpdateDialogAsync(UpdateInfo updateInfo)
        {
            // Simple message box for now - can be replaced with custom UI
            try
            {
                var messageBox = new Window
                {
                    Title = "Cập nhật mới",
                    Width = 450,
                    Height = 250,
                    CanResize = false,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
                };

                var stackPanel = new Avalonia.Controls.StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 15
                };

                stackPanel.Children.Add(new Avalonia.Controls.TextBlock
                {
                    Text = $"Phiên bản mới {updateInfo.Version} đã sẵn sàng!",
                    FontSize = 16,
                    FontWeight = Avalonia.Media.FontWeight.Bold
                });

                stackPanel.Children.Add(new Avalonia.Controls.TextBlock
                {
                    Text = $"Phiên bản hiện tại: {updateService.CurrentVersion}",
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.Gray
                });

                if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
                {
                    var scrollViewer = new Avalonia.Controls.ScrollViewer
                    {
                        Height = 80,
                        Content = new Avalonia.Controls.TextBlock
                        {
                            Text = updateInfo.ReleaseNotes,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 11
                        }
                    };
                    stackPanel.Children.Add(scrollViewer);
                }

                var buttonPanel = new Avalonia.Controls.StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 10
                };

                bool result = false;

                var updateButton = new Avalonia.Controls.Button
                {
                    Content = "Cập nhật ngay",
                    Padding = new Thickness(20, 8),
                    Background = Avalonia.Media.Brushes.Green,
                    Foreground = Avalonia.Media.Brushes.White
                };
                updateButton.Click += (s, e) => { result = true; messageBox.Close(); };

                var laterButton = new Avalonia.Controls.Button
                {
                    Content = "Để sau",
                    Padding = new Thickness(20, 8)
                };
                laterButton.Click += (s, e) => { result = false; messageBox.Close(); };

                buttonPanel.Children.Add(updateButton);
                buttonPanel.Children.Add(laterButton);
                stackPanel.Children.Add(buttonPanel);

                messageBox.Content = stackPanel;

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    await messageBox.ShowDialog(desktop.MainWindow);
                }

                return result;
            }
            catch
            {
                return false;
            }
        }

        private IUpdateService? updateService => _serviceProvider?.GetService<IUpdateService>();

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