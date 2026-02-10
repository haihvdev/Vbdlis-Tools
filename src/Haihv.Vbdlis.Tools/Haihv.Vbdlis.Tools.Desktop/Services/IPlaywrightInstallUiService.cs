using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    public interface IPlaywrightInstallUiService
    {
        Task<bool> EnsurePlaywrightBrowsersAsync(bool forceInstall = false);
    }
}