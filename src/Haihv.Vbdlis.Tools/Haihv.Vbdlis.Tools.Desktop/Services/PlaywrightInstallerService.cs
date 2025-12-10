using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for managing Playwright browser detection.
    /// Detects bundled browsers and copies them to the system cache when needed.
    /// </summary>
    public class PlaywrightInstallerService : IPlaywrightInstallerService
    {
        private readonly ILogger _logger;

        public PlaywrightInstallerService()
        {
            _logger = Log.ForContext<PlaywrightInstallerService>();
        }

        /// <summary>
        /// Gets the current operating system name
        /// </summary>
        public string GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "MacOS";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";

            return "Unknown";
        }

        /// <summary>
        /// Gets the Playwright browsers directory path based on OS
        /// </summary>
        private string GetPlaywrightBrowsersPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: %USERPROFILE%\AppData\Local\ms-playwright
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "ms-playwright");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // MacOS: ~/Library/Caches/ms-playwright
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Caches", "ms-playwright");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: ~/.cache/ms-playwright
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".cache", "ms-playwright");
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns all locations we will search for Playwright browsers.
        /// Order: explicit env var -> system cache -> app local directory.
        /// </summary>
        private string[] GetBrowserSearchPaths()
        {

            var paths = new List<string>
            {
                GetPlaywrightBrowsersPath(),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ms-playwright")
            };
            var envPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                paths.Add(envPath);
            }
            return [.. paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Decide where to install browsers. Prefer explicit env var, then system cache, then app-local.
        /// </summary>
        private string GetInstallTargetPath()
        {
            var envPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return Path.GetFullPath(envPath);
            }

            var systemCachePath = GetPlaywrightBrowsersPath();
            if (!string.IsNullOrWhiteSpace(systemCachePath))
            {
                return Path.GetFullPath(systemCachePath);
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ms-playwright");
        }

        /// <summary>
        /// Checks if Playwright browsers are installed.
        /// Does not bundle or copy – expects browsers in system cache or PLAYWRIGHT_BROWSERS_PATH.
        /// </summary>
        public bool IsBrowsersInstalled()
        {
            try
            {
                var searchPaths = GetBrowserSearchPaths();

                foreach (var path in searchPaths)
                {
                    if (HasChromiumInstalled(path))
                    {
                        _logger.Information("Playwright browsers found at: {Path}", path);
                        return true;
                    }
                }

                _logger.Warning(
                    "Playwright browsers not found. Searched: {Paths}. Run: playwright install chromium",
                    searchPaths.Length > 0 ? string.Join(", ", searchPaths) : "<none>");

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking Playwright browsers installation");
                return false;
            }
        }

        /// <summary>
        /// Ensures Playwright browsers exist; will run "playwright install chromium" if missing.
        /// </summary>
        public async Task<bool> EnsureBrowsersInstalledAsync(Action<string>? onStatusChange = null)
        {
            if (IsBrowsersInstalled())
            {
                return true;
            }

            try
            {
                var installTargetPath = GetInstallTargetPath();
                Directory.CreateDirectory(installTargetPath);

                // Ensure Playwright CLI installs to the same path we detect later
                var envPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
                if (string.IsNullOrWhiteSpace(envPath))
                {
                    Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", installTargetPath);
                    _logger.Information("PLAYWRIGHT_BROWSERS_PATH not set. Using: {Path}", installTargetPath);
                }
                else
                {
                    _logger.Information("PLAYWRIGHT_BROWSERS_PATH is preset. Using: {Path}", envPath);
                    installTargetPath = Path.GetFullPath(envPath);
                }

                _logger.Information("Playwright browsers missing. Attempting installation (chromium) to {Path}...", installTargetPath);
                onStatusChange?.Invoke("Đang tải và cài đặt Playwright (Chromium)...");
                // Try bundled scripts first (pwsh/bash), then fallback to Microsoft.Playwright.Program
                var installed = await RunPlaywrightInstallScriptAsync(installTargetPath, onStatusChange);
                if (!installed)
                {
                    _logger.Information("Falling back to Microsoft.Playwright.Program.Main install...");
                    onStatusChange?.Invoke("Đang cài đặt bằng Playwright CLI...");
                    await Task.Run(() =>
                    {
                        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", installTargetPath);
                        global::Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                    });
                }

                if (IsBrowsersInstalled())
                {
                    _logger.Information("Playwright browsers installed successfully.");
                    onStatusChange?.Invoke("Cài đặt Playwright hoàn tất");
                    return true;
                }

                _logger.Warning("Playwright installation finished but browsers still not detected.");
                onStatusChange?.Invoke("Cài đặt hoàn tất nhưng không tìm thấy Chromium. Vui lòng thử lại hoặc cài thủ công.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to install Playwright browsers");
                onStatusChange?.Invoke($"Lỗi khi cài đặt Playwright: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RunPlaywrightInstallScriptAsync(string installTargetPath, Action<string>? onStatusChange = null)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var psScript = Path.Combine(appDir, "playwright.ps1");
            var shScript = Path.Combine(appDir, "playwright.sh");

            _logger.Information("Running bundled Playwright installer targeting {Path}...", installTargetPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (File.Exists(psScript))
                {
                    // Try powershell.exe (Windows PowerShell 5.1 - built-in) first, then fallback to pwsh (PowerShell 7+)
                    onStatusChange?.Invoke("Đang cài đặt bằng Windows PowerShell...");
                    if (await RunProcessAsync("powershell", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{psScript}\" install chromium", appDir, installTargetPath, onStatusChange))
                    {
                        return true;
                    }

                    _logger.Information("powershell.exe failed or not found, trying pwsh...");
                    onStatusChange?.Invoke("Đang cài đặt bằng PowerShell Core...");
                    return await RunProcessAsync("pwsh", $"-NoProfile -NonInteractive -File \"{psScript}\" install chromium", appDir, installTargetPath, onStatusChange);
                }
                return false;
            }

            // macOS/Linux: prefer bash (no pwsh dependency), then pwsh as fallback
            if (File.Exists(shScript))
            {
                onStatusChange?.Invoke("Đang cài đặt bằng bash script...");
                if (await RunProcessAsync("bash", $"\"{shScript}\" install chromium", appDir, installTargetPath, onStatusChange))
                {
                    return true;
                }
            }

            if (File.Exists(psScript))
            {
                onStatusChange?.Invoke("Đang cài đặt bằng PowerShell...");
                return await RunProcessAsync("pwsh", $"-NoProfile -NonInteractive -File \"{psScript}\" install chromium", appDir, installTargetPath, onStatusChange);
            }

            return false;
        }

        private async Task<bool> RunProcessAsync(string fileName, string arguments, string workingDir, string? browserPath = null, Action<string>? onStatusChange = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrWhiteSpace(browserPath))
                {
                    psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = browserPath;
                }

                using var process = new Process { StartInfo = psi };
                process.Start();

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());

                var exitCode = process.ExitCode;
                var stdout = stdoutTask.Result;
                var stderr = stderrTask.Result;

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    onStatusChange?.Invoke(stdout);
                    _logger.Information("playwright install stdout: {Stdout}", stdout);
                }
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    onStatusChange?.Invoke(stderr);
                    _logger.Information("playwright install stderr: {Stderr}", stderr);
                }

                if (exitCode == 0)
                {
                    _logger.Information("playwright install completed successfully via {Exe}", fileName);
                    return true;
                }

                _logger.Warning("playwright install exited with code {Code} via {Exe}", exitCode, fileName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to run playwright install via {Exe}", fileName);
                return false;
            }
        }

        private bool HasChromiumInstalled(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return false;
            }

            try
            {
                var hasChromium = Directory.GetDirectories(path, "chromium-*").Length > 0;
                var hasHeadlessShell = Directory.GetDirectories(path, "chromium_headless_shell-*").Length > 0;
                var hasFfmpeg = Directory.GetDirectories(path, "ffmpeg-*").Length > 0;

                if (!hasChromium || !hasHeadlessShell || !hasFfmpeg)
                {
                    _logger.Warning("Chromium installation incomplete at {Path}. chromium: {HasChromium}, headless_shell: {HasHeadless}, ffmpeg: {HasFfmpeg}",
                        path, hasChromium, hasHeadlessShell, hasFfmpeg);
                }

                return hasChromium && hasHeadlessShell && hasFfmpeg;
            }
            catch
            {
                return false;
            }
        }

    }
}
