using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Haihv.Vbdlis.Tools.Desktop.Helpers;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;
using Haihv.Vbdlis.Tools.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    public class ApplicationInitializer(IServiceProvider serviceProvider) : IApplicationInitializer
    {
        private SplashWindow? _splashWindow;
        private SplashWindowViewModel? _splashViewModel;

        public bool IsShuttingDown { get; private set; }

        /// <summary>
        /// Initializes the main window after checking for updates and ensuring Playwright browsers are installed.
        /// </summary>
        public async Task InitializeAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            Log.Information("Starting main window initialization...");

            // Show splash screen
            _splashViewModel = new SplashWindowViewModel();
            _splashWindow = new SplashWindow
            {
                DataContext = _splashViewModel
            };
            _splashWindow.Show();

            try
            {
                // Update splash screen: Checking for updates
                await Dispatcher.UIThread.InvokeAsync(() => { _splashViewModel?.SetCheckingUpdates(); });

                // For testing: Force update dialog
                // await HandleForceUpdateTestAsync(desktop);
                // return;

                // First, check for updates BEFORE starting the app
                await CheckForUpdatesAsync();

                // Check if app is shutting down after update check
                if (IsShuttingDown)
                {
                    Log.Information("Application is shutting down for update, skipping main window initialization");
                    await CloseSplashWindowAsync();
                    return;
                }

                // Update splash screen: Checking Playwright
                await Dispatcher.UIThread.InvokeAsync(() => { _splashViewModel?.SetCheckingPlaywright(); });

                // Then, ensure Playwright browsers are installed
                await EnsurePlaywrightBrowsersAsync();

                Log.Information("Playwright check completed, preparing to show main window...");

                // Check if app is shutting down before proceeding
                if (IsShuttingDown)
                {
                    Log.Information("Application is shutting down, skipping main window initialization");
                    await CloseSplashWindowAsync();
                    return;
                }

                // Update splash screen: Initializing main window
                await Dispatcher.UIThread.InvokeAsync(() => { _splashViewModel?.SetInitializingMainWindow(); });

                // Small delay to show the status
                await Task.Delay(300);

                // Then create and show the main window on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (IsShuttingDown)
                    {
                        Log.Error("App is shutting down, cannot initialize main window");
                        return;
                    }

                    Log.Information("Creating MainWindow and ViewModel...");

                    // Get MainWindowViewModel from DI container
                    var mainViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();

                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = mainViewModel,
                        MinWidth = 1100,
                        MinHeight = 880,
                        WindowState = WindowState.Maximized,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };

                    // Update splash screen: Complete
                    _splashViewModel?.SetComplete();

                    // Show the main window
                    desktop.MainWindow.Show();

                    // Close splash window
                    _splashWindow?.Close();

                    // Now that MainWindow is shown, change shutdown mode to close when main window closes
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

                    Log.Information("Main window initialized and shown");
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize main window");
                await CloseSplashWindowAsync();
                throw;
            }
        }

        /// <summary>
        /// Handles force update test mode
        /// </summary>
        private async Task HandleForceUpdateTestAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var updateService = serviceProvider.GetService<IUpdateService>();
            var currentVersion = updateService?.CurrentVersion ?? "N/A";

            var releaseNotes = await FetchLatestReleaseNotesFromGithubAsync();
            if (string.IsNullOrWhiteSpace(releaseNotes))
            {
                releaseNotes = "### ✨ Có gì mới trong phiên bản này?\n" +
                               "- Sửa lỗi hiển thị ghi chú phát hành\n" +
                               "- Cải thiện **hiệu suất**\n" +
                               "\n" +
                               "### 📝 Ghi chú\n" +
                               "- Đây là dữ liệu test (force update).";
            }

            var sample = new UpdateInfo
            {
                Version = currentVersion,
                FileSize = 0,
                PublishedAt = DateTime.Now,
                IsRequired = true,
                DownloadUrl = string.Empty,
                ReleaseNotes = releaseNotes
            };

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialogService = serviceProvider.GetRequiredService<IUpdateDialogService>();
                await dialogService.ShowUpdateDialogAsync(sample, currentVersion, allowLater: false);
                _splashWindow?.Close();
                desktop.Shutdown();
            });
        }

        /// <summary>
        /// Checks if Playwright browsers are available; attempts install if missing.
        /// </summary>
        private async Task EnsurePlaywrightBrowsersAsync()
        {
            var installer = serviceProvider.GetService<IPlaywrightInstallerService>();
            if (installer == null)
            {
                Log.Warning("PlaywrightInstallerService is not registered");
                return;
            }

            var installUiService = serviceProvider.GetService<IPlaywrightInstallUiService>();
            if (installUiService == null)
            {
                Log.Warning("PlaywrightInstallUiService is not registered");
                return;
            }

            var os = installer.GetOperatingSystem();
            Log.Information("Checking Playwright browsers on {OS}...", os);

            // If already installed, we're done
            if (installer.IsBrowsersInstalled())
            {
                Log.Information("Playwright browsers are ready.");
                return;
            }

            // Update splash screen for installing Playwright
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _splashViewModel?.UpdateStatus("Cần cài đặt Playwright browsers...", 65);
            });

            bool ready;
            try
            {
                ready = await installUiService.EnsurePlaywrightBrowsersAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while ensuring Playwright browsers");
                ready = false;
            }

            if (ready)
            {
                Log.Information("Playwright browsers are ready.");
            }
            else
            {
                Log.Warning("Playwright browsers still missing after attempted install.");
            }
        }

        /// <summary>
        /// Checks for application updates before app starts
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            Log.Information("[AUTO-UPDATE] Bắt đầu kiểm tra cập nhật tự động");

            try
            {
                var updateService = serviceProvider.GetService<IUpdateService>();
                if (updateService == null)
                {
                    Log.Warning("[AUTO-UPDATE] UpdateService không được đăng ký");
                    return;
                }

                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo != null)
                {
                    Log.Information("[AUTO-UPDATE] Hiển thị dialog cập nhật: {Version}", updateInfo.Version);

                    // Show update notification on UI thread
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var dialogService = serviceProvider.GetRequiredService<IUpdateDialogService>();
                        var result = await dialogService.ShowUpdateDialogAsync(
                            updateInfo,
                            updateService.CurrentVersion);

                        if (result)
                        {
                            Log.Information("[AUTO-UPDATE] User chọn: CẬP NHẬT NGAY");
                            await HandleUpdateDownloadAsync(updateInfo, updateService);
                        }
                        else
                        {
                            Log.Information("[AUTO-UPDATE] User chọn: ĐỂ SAU");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AUTO-UPDATE] Lỗi: {Message}", ex.Message);
            }

            Log.Information("[AUTO-UPDATE] Kết thúc kiểm tra");
        }

        /// <summary>
        /// Handles update download and installation
        /// </summary>
        private async Task HandleUpdateDownloadAsync(UpdateInfo updateInfo, IUpdateService updateService)
        {
            var dialogService = serviceProvider.GetRequiredService<IUpdateDialogService>();
            var (updateProgress, updateStatus, closeWindow) = dialogService.ShowProgressWindow();

            var success = await updateService.DownloadAndInstallUpdateAsync(
                updateInfo,
                progress =>
                {
                    Log.Debug("[AUTO-UPDATE] Progress: {Progress}%", progress);
                    Dispatcher.UIThread.Post(() =>
                    {
                        updateProgress(progress);
                        updateStatus($"Đang tải bản cập nhật... {progress}%");
                    });
                },
                async () =>
                {
                    // Before restart, show final message and wait a moment
                    for (var i = 3; i >= 1; i--)
                    {
                        var i1 = i;
                        Dispatcher.UIThread.Post(() =>
                        {
                            updateProgress(100);
                            updateStatus($"Tải xong. Ứng dụng sẽ khởi động lại sau {i1} giây...");
                        });
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                });

            if (success)
            {
                Log.Information("[AUTO-UPDATE] Thành công - App sẽ restart");
                IsShuttingDown = true;
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    updateStatus("Tải/cài đặt thất bại. Vui lòng thử lại.");
                    closeWindow();
                });
                Log.Error("[AUTO-UPDATE] Thất bại - Vui lòng thử lại");
            }
        }

        /// <summary>
        /// Fetches latest release notes from GitHub
        /// </summary>
        private static async Task<string> FetchLatestReleaseNotesFromGithubAsync()
        {
            try
            {
                const string owner = "haitnmt";
                const string repo = "Vbdlis-Tools";
                var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Vbdlis-Tools-UpdateDialogTest");

                var json = await client.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("body", out var bodyElement))
                {
                    var body = bodyElement.GetString() ?? string.Empty;
                    return MarkdownHelper.ExtractAppUpdateNotes(body);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TEST] Failed to fetch GitHub release notes");
                return string.Empty;
            }
        }

        /// <summary>
        /// Closes splash window
        /// </summary>
        private async Task CloseSplashWindowAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { _splashWindow?.Close(); });
        }
    }
}