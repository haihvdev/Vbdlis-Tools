using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for managing Playwright browser installation.
    /// Automatically installs Playwright browsers on first run for Windows and MacOS.
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
        /// Checks if auto-installation is supported on the current OS
        /// Linux is not supported yet as per requirements
        /// </summary>
        public bool IsAutoInstallSupported()
        {
            var os = GetOperatingSystem();
            return os == "Windows" || os == "MacOS";
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
        /// Checks if Playwright browsers are installed by verifying the browsers directory
        /// </summary>
        public bool IsBrowsersInstalled()
        {
            try
            {
                var browsersPath = GetPlaywrightBrowsersPath();

                if (string.IsNullOrEmpty(browsersPath))
                {
                    _logger.Warning("Could not determine Playwright browsers path for OS: {OS}", GetOperatingSystem());
                    return false;
                }

                // Check if the directory exists and contains browser files
                if (!Directory.Exists(browsersPath))
                {
                    _logger.Information("Playwright browsers directory not found at: {Path}", browsersPath);
                    return false;
                }

                // Check if there are any browser directories (chromium-*, firefox-*, webkit-*)
                var browserDirs = Directory.GetDirectories(browsersPath, "chromium-*");

                if (browserDirs.Length == 0)
                {
                    _logger.Information("No Chromium browser found in: {Path}", browsersPath);
                    return false;
                }

                _logger.Information("Playwright browsers found at: {Path}", browsersPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking Playwright browsers installation");
                return false;
            }
        }

        /// <summary>
        /// Installs Playwright browsers using Microsoft.Playwright.Program with progress tracking
        /// </summary>
        public async Task<bool> InstallBrowsersAsync(Action<string>? progress = null)
        {
            try
            {
                var os = GetOperatingSystem();
                _logger.Information("Starting Playwright browsers installation on {OS}", os);
                progress?.Invoke($"Đang khởi tạo cài đặt Playwright...");

                // Redirect console output to capture progress
                var originalOut = Console.Out;
                var originalError = Console.Error;
                var outputBuilder = new StringBuilder();

                await Task.Run(() =>
                {
                    try
                    {
                        using var stringWriter = new StringWriter(outputBuilder);
                        Console.SetOut(stringWriter);
                        Console.SetError(stringWriter);

                        // Track last reported percentage to avoid duplicate updates
                        int lastPercent = -1;
                        string lastMessage = "";

                        // Periodically check output buffer for progress updates
                        var timer = new System.Threading.Timer(_ =>
                        {
                            var output = outputBuilder.ToString();
                            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                            if (lines.Length > 0)
                            {
                                var lastLine = lines[^1];

                                // Parse and report progress
                                var progressMessage = ParseInstallProgress(lastLine);
                                if (!string.IsNullOrEmpty(progressMessage) && progressMessage != lastMessage)
                                {
                                    lastMessage = progressMessage;
                                    _logger.Information("[Playwright] {Message}", progressMessage);
                                    progress?.Invoke(progressMessage);

                                    // Extract percentage if available
                                    var match = Regex.Match(progressMessage, @"(\d+)%");
                                    if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
                                    {
                                        if (percent != lastPercent)
                                        {
                                            lastPercent = percent;
                                        }
                                    }
                                }
                            }
                        }, null, 0, 500); // Check every 500ms

                        progress?.Invoke("Đang tải xuống Chromium browser...");
                        _logger.Information("Executing: playwright install chromium");

                        // Call Playwright CLI to install browsers
                        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });

                        timer.Dispose();

                        // Restore original console
                        Console.SetOut(originalOut);
                        Console.SetError(originalError);

                        if (exitCode != 0)
                        {
                            _logger.Error("Playwright installation failed with exit code: {ExitCode}", exitCode);
                            throw new InvalidOperationException($"Playwright installation failed with exit code: {exitCode}");
                        }

                        _logger.Information("Playwright installation completed successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.SetOut(originalOut);
                        Console.SetError(originalError);
                        _logger.Error(ex, "Error during Playwright installation");
                        throw;
                    }
                });

                progress?.Invoke("Cài đặt Playwright browsers hoàn tất!");
                _logger.Information("Playwright browsers installed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to install Playwright browsers");
                progress?.Invoke($"Lỗi: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses installation progress from Playwright output
        /// </summary>
        private string ParseInstallProgress(string output)
        {
            // Match patterns like "Downloading Chromium 123.0.4567.8"
            var downloadMatch = Regex.Match(output, @"Downloading (.+?) from");
            if (downloadMatch.Success)
            {
                return $"Đang tải xuống {downloadMatch.Groups[1].Value}...";
            }

            // Match patterns like "|■■■■■■■■                    |  10% of 89.7 MiB"
            var percentMatch = Regex.Match(output, @"\|\s*(.+?)\s*\|\s*(\d+)%\s+of\s+([\d.]+\s+\w+)");
            if (percentMatch.Success)
            {
                var percent = percentMatch.Groups[2].Value;
                var size = percentMatch.Groups[3].Value;
                return $"Đang tải xuống... {percent}% / {size}";
            }

            // Match patterns like "Chromium 123.0.4567.8 downloaded to /path/..."
            var completedMatch = Regex.Match(output, @"(.+?)\s+downloaded to");
            if (completedMatch.Success)
            {
                return $"Đã tải xong {completedMatch.Groups[1].Value}";
            }

            // Return original message for other important messages
            if (output.Contains("downloaded") || output.Contains("installed") ||
                output.Contains("Installing") || output.Contains("Extracting"))
            {
                return output;
            }

            return string.Empty;
        }

        /// <summary>
        /// Ensures Playwright browsers are installed.
        /// If not installed and OS is Windows/MacOS, installs them automatically.
        /// </summary>
        public async Task<bool> EnsureBrowsersInstalledAsync(Action<string>? progress = null)
        {
            var os = GetOperatingSystem();
            _logger.Information("Checking Playwright browsers installation on {OS}", os);

            // Check if already installed
            if (IsBrowsersInstalled())
            {
                _logger.Information("Playwright browsers already installed");
                progress?.Invoke("Playwright browsers đã được cài đặt");
                return true;
            }

            // Check if auto-install is supported
            if (!IsAutoInstallSupported())
            {
                var message = $"Tự động cài đặt Playwright chưa được hỗ trợ trên {os}. Vui lòng cài đặt thủ công.";
                _logger.Warning(message);
                progress?.Invoke(message);
                return false;
            }

            // Auto-install for Windows and MacOS
            _logger.Information("Playwright browsers not found. Starting auto-installation...");
            progress?.Invoke($"Chưa phát hiện Playwright browsers. Đang tự động cài đặt cho {os}...");

            var result = await InstallBrowsersAsync(progress);

            if (result)
            {
                _logger.Information("Auto-installation completed successfully");
            }
            else
            {
                _logger.Error("Auto-installation failed");
            }

            return result;
        }
    }
}
