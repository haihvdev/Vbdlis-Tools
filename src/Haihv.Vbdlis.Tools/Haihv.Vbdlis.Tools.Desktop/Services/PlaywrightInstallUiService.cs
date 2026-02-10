using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;
using Haihv.Vbdlis.Tools.Desktop.Views;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    public class PlaywrightInstallUiService : IPlaywrightInstallUiService
    {
        private readonly IPlaywrightInstallerService _installerService;
        private readonly ILogger _logger = Log.ForContext<PlaywrightInstallUiService>();

        public PlaywrightInstallUiService(IPlaywrightInstallerService installerService)
        {
            _installerService = installerService;
        }

        public async Task<bool> EnsurePlaywrightBrowsersAsync(bool forceInstall = false)
        {
            if (!forceInstall && _installerService.IsBrowsersInstalled())
            {
                return true;
            }

            var os = _installerService.GetOperatingSystem();
            PlaywrightInstallationWindow? installWindow = null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var installViewModel = new PlaywrightInstallationViewModel
                {
                    OperatingSystem = os
                };
                installWindow = new PlaywrightInstallationWindow
                {
                    DataContext = installViewModel,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                installWindow.Show();
                installWindow.StartInstallation();
            });

            if (installWindow == null)
            {
                return false;
            }

            bool ready;
            try
            {
                var showTerminalWindow = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                ready = await _installerService.EnsureBrowsersInstalledAsync(
                    message => { installWindow.UpdateStatus(message); }, showTerminalWindow, forceInstall);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while ensuring Playwright browsers");
                ready = false;
                installWindow.SetError(ex.Message);
            }

            if (ready)
            {
                installWindow.CompleteInstallation();
                _ = installWindow.AutoCloseAfterDelayAsync();
                return true;
            }

            installWindow.SetError("Không thể cài đặt Playwright. Vui lòng kiểm tra kết nối mạng hoặc cài thủ công.");
            return false;
        }
    }
}