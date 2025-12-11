using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Haihv.Vbdlis.Tools.Desktop.Views;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Serilog;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;

namespace Haihv.Vbdlis.Tools.Desktop
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;
        private bool _isShuttingDown = false;

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

                // Set ShutdownMode to OnExplicitShutdown to prevent auto-shutdown when windows close
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

                // Initialize MainWindow asynchronously after ensuring Playwright is ready
                _ = InitializeMainWindowAsync(desktop);

                // Handle application exit to cleanup resources
                desktop.ShutdownRequested += OnShutdownRequested;
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Initializes the main window after checking for updates and ensuring Playwright browsers are installed.
        /// This ensures the application is fully ready before showing the UI.
        /// </summary>
        private async Task InitializeMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            Log.Information("Starting main window initialization...");

            // First, check for updates BEFORE starting the app
            await CheckForUpdatesAsync();

            // Check if app is shutting down after update check (user may have chosen to update)
            if (_isShuttingDown)
            {
                Log.Information("Application is shutting down for update, skipping main window initialization");
                return;
            }

            // Then, ensure Playwright browsers are installed
            await EnsurePlaywrightBrowsersAsync();

            Log.Information("Playwright check completed, preparing to show main window...");

            // Check if app is shutting down before proceeding
            if (_isShuttingDown)
            {
                Log.Information("Application is shutting down, skipping main window initialization");
                return;
            }

            // Then create and show the main window on UI thread
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_serviceProvider == null || _isShuttingDown)
                    {
                        Log.Error("Service provider is null or app is shutting down, cannot initialize main window");
                        return;
                    }

                    Log.Information("Creating MainWindow and ViewModel...");

                    // Get MainWindowViewModel from DI container
                    var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = mainViewModel,
                        MinWidth = 1100,
                        MinHeight = 880,
                        WindowState = WindowState.Maximized,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };

                    // Show the main window
                    desktop.MainWindow.Show();

                    // Now that MainWindow is shown, change shutdown mode to close when main window closes
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

                    Log.Information("Main window initialized and shown");
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize main window");
                throw;
            }
        }

        private static void ConfigureServices(ServiceCollection services)
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
        /// Checks if Playwright browsers are available; attempts install if missing.
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
            Log.Information("Checking Playwright browsers on {OS}...", os);

            // If already installed, we're done
            if (installer.IsBrowsersInstalled())
            {
                Log.Information("Playwright browsers are ready.");
                return;
            }

            // Show installation window with progress/status
            var installViewModel = new PlaywrightInstallationViewModel
            {
                OperatingSystem = os
            };
            var installWindow = new PlaywrightInstallationWindow
            {
                DataContext = installViewModel,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            installWindow.Show();
            installWindow.StartInstallation();

            bool ready;
            try
            {
                ready = await installer.EnsureBrowsersInstalledAsync(message =>
                {
                    installWindow.UpdateStatus(message);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while ensuring Playwright browsers");
                ready = false;
                installWindow.SetError(ex.Message);
            }

            if (ready)
            {
                installWindow.CompleteInstallation();
                _ = installWindow.AutoCloseAfterDelayAsync();
                Log.Information("Playwright browsers are ready.");
            }
            else
            {
                installWindow.SetError("Không thể cài đặt Playwright. Vui lòng kiểm tra kết nối mạng hoặc cài thủ công.");
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
                if (_serviceProvider == null)
                {
                    Log.Warning("[AUTO-UPDATE] ServiceProvider chưa khởi tạo");
                    return;
                }

                var updateService = _serviceProvider.GetService<IUpdateService>();
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
                        var result = await ShowUpdateDialogAsync(updateInfo);

                        if (result)
                        {
                            Log.Information("[AUTO-UPDATE] User chọn: CẬP NHẬT NGAY");

                            var (progressWindow, progressBar, progressText) = CreateUpdateProgressWindow();
                            progressWindow.Show();

                            var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo,
                                progress =>
                                {
                                    Log.Debug("[AUTO-UPDATE] Progress: {Progress}%", progress);

                                    Dispatcher.UIThread.Post(() =>
                                    {
                                        progressBar.Value = progress;
                                        progressText.Text = $"Đang tải bản cập nhật... {progress}%";
                                    });
                                },
                                async () =>
                                {
                                    // Before restart, show final message and wait a moment
                                    for (int i = 3; i >= 1; i--)
                                    {
                                        Dispatcher.UIThread.Post(() =>
                                        {
                                            progressBar.Value = 100;
                                            progressText.Text = $"Tải xong. Ứng dụng sẽ khởi động lại sau {i} giây...";
                                        });
                                        await Task.Delay(TimeSpan.FromSeconds(1));
                                    }
                                });

                            if (success)
                            {
                                Log.Information("[AUTO-UPDATE] Thành công - App sẽ restart");
                            }
                            else
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    progressText.Text = "Tải/cài đặt thất bại. Vui lòng thử lại.";
                                    progressWindow.Close();
                                });
                                Log.Error("[AUTO-UPDATE] Thất bại - Vui lòng thử lại");
                            }
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
        /// Shows update dialog to user
        /// </summary>
        private async Task<bool> ShowUpdateDialogAsync(UpdateInfo updateInfo)
        {
            try
            {
                var messageBox = new Window
                {
                    Title = "Cập nhật mới",
                    Width = 520,
                    Height = 340,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                var container = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    Margin = new Thickness(20)
                };

                var headerPanel = new StackPanel
                {
                    Spacing = 6,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                headerPanel.Children.Add(new TextBlock
                {
                    Text = "Có bản cập nhật mới",
                    FontSize = 18,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    TextAlignment = Avalonia.Media.TextAlignment.Center
                });

                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"Phiên bản hiện tại: {updateService?.CurrentVersion ?? "N/A"}",
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.Gray,
                    TextAlignment = Avalonia.Media.TextAlignment.Center
                });

                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"Phiên bản mới: {updateInfo.Version}",
                    FontSize = 14,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    TextAlignment = Avalonia.Media.TextAlignment.Center
                });

                Grid.SetRow(headerPanel, 0);
                container.Children.Add(headerPanel);

                var releaseNotesText = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
                    ? "Không có ghi chú phát hành."
                    : updateInfo.ReleaseNotes;

                var infoPanel = new Border
                {
                    BorderThickness = new Thickness(1),
                    BorderBrush = Avalonia.Media.Brushes.LightGray,
                    Background = Avalonia.Media.Brushes.White,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14)
                };

                var infoStack = new StackPanel { Spacing = 10 };

                infoStack.Children.Add(new TextBlock
                {
                    Text = "Nội dung cập nhật",
                    FontSize = 13,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold
                });

                infoStack.Children.Add(new ScrollViewer
                {
                    Height = 150,
                    Content = new TextBlock
                    {
                        Text = releaseNotesText,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 12
                    }
                });

                if (updateInfo.FileSize > 0)
                {
                    var sizeMb = updateInfo.FileSize / 1024.0 / 1024.0;
                    infoStack.Children.Add(new TextBlock
                    {
                        Text = $"Dung lượng ước tính: {sizeMb:F1} MB",
                        FontSize = 11,
                        Foreground = Avalonia.Media.Brushes.Gray
                    });
                }

                infoPanel.Child = infoStack;
                Grid.SetRow(infoPanel, 1);
                container.Children.Add(infoPanel);

                var buttonPanel = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 12,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                bool result = false;

                var updateButton = new Button
                {
                    Content = "Cập nhật ngay",
                    Padding = new Thickness(16, 10),
                    Background = Avalonia.Media.Brushes.ForestGreen,
                    Foreground = Avalonia.Media.Brushes.White
                };
                updateButton.Click += (s, e) =>
                {
                    result = true;
                    messageBox.Close();
                };
                Grid.SetColumn(updateButton, 0);

                var laterButton = new Button
                {
                    Content = "Để sau",
                    Padding = new Thickness(16, 10)
                };
                laterButton.Click += (s, e) =>
                {
                    result = false;
                    messageBox.Close();
                };
                Grid.SetColumn(laterButton, 1);

                buttonPanel.Children.Add(updateButton);
                buttonPanel.Children.Add(laterButton);
                Grid.SetRow(buttonPanel, 2);
                container.Children.Add(buttonPanel);

                messageBox.Content = container;

                // Show as standalone window and wait for it to close
                var tcs = new TaskCompletionSource<bool>();
                messageBox.Closed += (s, e) => tcs.SetResult(result);
                messageBox.Show();

                return await tcs.Task;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a small progress window for update download/install steps
        /// </summary>
        private (Window window, ProgressBar progressBar, TextBlock statusText) CreateUpdateProgressWindow()
        {
            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 14
            };

            var statusText = new TextBlock
            {
                Text = "Đang chuẩn bị...",
                FontSize = 12,
                Foreground = Avalonia.Media.Brushes.Gray
            };

            var stackPanel = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(20)
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Đang tải bản cập nhật",
                FontSize = 16,
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });

            stackPanel.Children.Add(progressBar);
            stackPanel.Children.Add(statusText);

            var window = new Window
            {
                Title = "Đang cập nhật",
                Width = 420,
                Height = 200,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = stackPanel
            };

            return (window, progressBar, statusText);
        }

        private IUpdateService? updateService => _serviceProvider?.GetService<IUpdateService>();

        private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            _isShuttingDown = true;

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
