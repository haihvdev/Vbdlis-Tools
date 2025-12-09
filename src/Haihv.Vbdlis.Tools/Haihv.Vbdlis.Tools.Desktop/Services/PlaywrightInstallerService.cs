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
            TextWriter? originalOut = null;
            TextWriter? originalError = null;
            System.Threading.Timer? timer = null;

            try
            {
                var os = GetOperatingSystem();
                _logger.Information("Starting Playwright browsers installation on {OS}", os);
                progress?.Invoke($"Đang khởi tạo cài đặt Playwright...");

                var outputBuilder = new StringBuilder();

                await Task.Run(() =>
                {
                    try
                    {
                        // Save original console streams
                        originalOut = Console.Out;
                        originalError = Console.Error;

                        // Track last reported percentage to avoid duplicate updates
                        int lastPercent = -1;
                        string lastMessage = "";

                        // Redirect console output to capture progress
                        var stringWriter = new StringWriter(outputBuilder);
                        Console.SetOut(stringWriter);
                        Console.SetError(stringWriter);

                        // Periodically check output buffer for progress updates
                        timer = new System.Threading.Timer(_ =>
                        {
                            try
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
                                        originalOut?.WriteLine($"[Playwright] {progressMessage}");
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
                            }
                            catch (Exception timerEx)
                            {
                                _logger.Warning(timerEx, "Error in progress timer callback");
                            }
                        }, null, 0, 500); // Check every 500ms

                        progress?.Invoke("Đang tải xuống Chromium browser...");
                        _logger.Information("Executing: playwright install chromium");

                        // Log environment information
                        var currentDir = Directory.GetCurrentDirectory();
                        var appDir = AppDomain.CurrentDomain.BaseDirectory;
                        _logger.Information("Current directory: {Dir}", currentDir);
                        _logger.Information("App base directory: {AppDir}", appDir);
                        _logger.Information("User profile: {Profile}", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                        _logger.Information("Playwright cache path: {Path}", GetPlaywrightBrowsersPath());
                        _logger.Information("OS: {OS}", GetOperatingSystem());

                        // macOS: Log permissions and environment
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            var libraryCaches = Path.Combine(homeDir, "Library", "Caches");

                            _logger.Information("HOME directory: {Home}", homeDir);
                            _logger.Information("Library/Caches: {Path}, Exists: {Exists}",
                                libraryCaches, Directory.Exists(libraryCaches));

                            // Check .playwright folder in app bundle
                            var playwrightInApp = Path.Combine(appDir, ".playwright");
                            if (Directory.Exists(playwrightInApp))
                            {
                                _logger.Information("Found .playwright in app bundle: {Path}", playwrightInApp);

                                // List files in .playwright to verify structure
                                try
                                {
                                    var files = Directory.GetFiles(playwrightInApp, "*", SearchOption.AllDirectories);
                                    _logger.Information(".playwright contains {Count} files", files.Length);

                                    // Check for node executable
                                    var nodeExe = Path.Combine(playwrightInApp, "node", "mac", "node");
                                    if (File.Exists(nodeExe))
                                    {
                                        _logger.Information("Found node executable: {Path}", nodeExe);

                                        // Ensure node executable has execute permission on macOS
                                        try
                                        {
                                            var chmodProcess = new Process
                                            {
                                                StartInfo = new ProcessStartInfo
                                                {
                                                    FileName = "chmod",
                                                    Arguments = $"+x \"{nodeExe}\"",
                                                    RedirectStandardOutput = true,
                                                    RedirectStandardError = true,
                                                    UseShellExecute = false,
                                                    CreateNoWindow = true
                                                }
                                            };
                                            chmodProcess.Start();
                                            chmodProcess.WaitForExit(3000);

                                            if (chmodProcess.ExitCode == 0)
                                            {
                                                _logger.Information("Set execute permission on node executable");
                                            }
                                            else
                                            {
                                                _logger.Warning("Failed to chmod +x node executable: {Error}",
                                                    chmodProcess.StandardError.ReadToEnd());
                                            }
                                        }
                                        catch (Exception chmodEx)
                                        {
                                            _logger.Warning(chmodEx, "Could not set execute permission on node (not critical)");
                                        }
                                    }
                                    else
                                    {
                                        _logger.Warning("Node executable NOT found at: {Path}", nodeExe);
                                    }
                                }
                                catch (Exception listEx)
                                {
                                    _logger.Warning(listEx, "Could not list .playwright contents");
                                }
                            }
                            else
                            {
                                _logger.Error(".playwright folder NOT found in app bundle at: {Path}", playwrightInApp);
                            }

                            // Check environment variables
                            var envVars = new[] { "HOME", "USER", "TMPDIR", "PATH", "PLAYWRIGHT_BROWSERS_PATH" };
                            foreach (var envVar in envVars)
                            {
                                var value = Environment.GetEnvironmentVariable(envVar);
                                _logger.Information("ENV {Var} = {Value}", envVar, value ?? "(not set)");
                            }
                        }

                        // Change to app directory to ensure Playwright can find its assets
                        var originalDir = currentDir;
                        try
                        {
                            Directory.SetCurrentDirectory(appDir);
                            _logger.Information("Changed working directory to: {AppDir}", appDir);

                            // Check if .playwright directory exists in app directory
                            var playwrightDriverPath = Path.Combine(appDir, ".playwright");
                            if (Directory.Exists(playwrightDriverPath))
                            {
                                _logger.Information("Found .playwright driver at: {Path}", playwrightDriverPath);
                            }
                            else
                            {
                                _logger.Warning(".playwright driver not found at: {Path}", playwrightDriverPath);
                            }

                            // Call Playwright CLI to install browsers
                            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });

                            _logger.Information("Playwright install command completed with exit code: {ExitCode}", exitCode);

                            if (exitCode != 0)
                            {
                                _logger.Error("Playwright installation failed with exit code: {ExitCode}", exitCode);
                                _logger.Error("Installation output: {Output}", outputBuilder.ToString());
                                throw new InvalidOperationException($"Playwright installation failed with exit code: {exitCode}");
                            }
                        }
                        finally
                        {
                            // Restore original directory
                            Directory.SetCurrentDirectory(originalDir);
                        }

                        _logger.Information("Playwright installation completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error during Playwright installation. Output so far: {Output}", outputBuilder.ToString());
                        throw;
                    }
                    finally
                    {
                        // Cleanup timer
                        timer?.Dispose();
                        timer = null;

                        // Restore original console
                        if (originalOut != null)
                        {
                            Console.SetOut(originalOut);
                        }
                        if (originalError != null)
                        {
                            Console.SetError(originalError);
                        }
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

                // Ensure console is restored
                try
                {
                    if (originalOut != null) Console.SetOut(originalOut);
                    if (originalError != null) Console.SetError(originalError);
                    timer?.Dispose();
                }
                catch { }

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
