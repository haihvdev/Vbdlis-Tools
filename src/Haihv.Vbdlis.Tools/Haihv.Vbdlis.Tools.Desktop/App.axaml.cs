using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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

                // Check and install Playwright browsers if needed (async, non-blocking)
                // This runs in background and logs progress
                _ = EnsurePlaywrightBrowsersAsync();

                // Get MainWindowViewModel from DI container
                var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                    MinWidth = 1100,
                    MinHeight = 880,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen
                };

                // Handle application exit to cleanup resources
                desktop.ShutdownRequested += OnShutdownRequested;
            }

            base.OnFrameworkInitializationCompleted();
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
        /// Supported on Windows and MacOS only.
        /// </summary>
        private async Task EnsurePlaywrightBrowsersAsync()
        {
            try
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

                // Ensure browsers are installed (will auto-install if needed)
                var success = await installer.EnsureBrowsersInstalledAsync(message =>
                {
                    Log.Information("[Playwright] {Message}", message);
                });

                if (success)
                {
                    Log.Information("Playwright browsers are ready to use");
                }
                else
                {
                    Log.Error("Failed to ensure Playwright browsers are installed. The application may not function correctly.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Playwright browsers installation check");
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