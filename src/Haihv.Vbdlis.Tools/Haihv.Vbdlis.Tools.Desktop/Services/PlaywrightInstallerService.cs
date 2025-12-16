using System;
using System.Collections.Generic;
using System.ComponentModel;
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
                // Try bundled scripts first (pwsh/bash), then dotnet CLI, then in-process CLI.
                var installed = await RunPlaywrightInstallScriptAsync(installTargetPath, onStatusChange);
                var hasBrowsersAfterScript = IsBrowsersInstalled();

                if (!installed || !hasBrowsersAfterScript)
                {
                    if (installed)
                    {
                        _logger.Warning("Bundled installer reported success but browsers are still missing. Trying dotnet CLI fallback.");
                    }
                    else
                    {
                        _logger.Information("Bundled installer failed or unavailable, trying dotnet CLI fallback...");
                    }

                    var dotnetInstalled = await RunPlaywrightDotnetCliAsync(installTargetPath, onStatusChange);
                    var hasBrowsersAfterDotnet = IsBrowsersInstalled();

                    if (!dotnetInstalled || !hasBrowsersAfterDotnet)
                    {
                        _logger.Information("dotnet CLI fallback failed or browsers still missing. Falling back to Microsoft.Playwright.Program.Main install...");
                        onStatusChange?.Invoke("Đang cài đặt bằng Playwright CLI (in-process)...");
                        await RunPlaywrightProgramMainAsync(installTargetPath, onStatusChange);
                    }
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

        private async Task<bool> RunPlaywrightDotnetCliAsync(string installTargetPath, Action<string>? onStatusChange = null)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var playwrightDll = Path.Combine(appDir, "Microsoft.Playwright.dll");
            if (!File.Exists(playwrightDll))
            {
                _logger.Warning("Microsoft.Playwright.dll not found at {DllPath}. Skipping dotnet CLI fallback.", playwrightDll);
                return false;
            }

            _logger.Information("Running dotnet CLI Playwright installer targeting {Path} using {Dll}...", installTargetPath, playwrightDll);
            onStatusChange?.Invoke("Đang cài đặt bằng dotnet Playwright CLI...");

            var args = $"\"{playwrightDll}\" install chromium";
            var success = await RunProcessAsync("dotnet", args, appDir, installTargetPath, onStatusChange, TimeSpan.FromMinutes(7));

            if (success)
            {
                _logger.Information("dotnet CLI Playwright install completed.");
            }
            else
            {
                _logger.Warning("dotnet CLI Playwright installer failed or timed out.");
            }

            return success;
        }

        private async Task<bool> RunPlaywrightProgramMainAsync(string installTargetPath, Action<string>? onStatusChange = null)
        {
            try
            {
                _logger.Information("Running Microsoft.Playwright.Program.Main install targeting {Path}...", installTargetPath);
                onStatusChange?.Invoke("Đang cài đặt bằng Playwright CLI (in-process)...");
                var sw = Stopwatch.StartNew();

                var exitCode = await Task.Run(() =>
                {
                    Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", installTargetPath);
                    return global::Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                });

                sw.Stop();
                if (exitCode == 0)
                {
                    _logger.Information("Microsoft.Playwright.Program.Main completed in {Elapsed}.", sw.Elapsed);
                    return true;
                }

                _logger.Warning("Microsoft.Playwright.Program.Main exited with code {Code} after {Elapsed}.", exitCode, sw.Elapsed);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Microsoft.Playwright.Program.Main failed");
                onStatusChange?.Invoke($"Lỗi khi chạy Playwright CLI: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RunPlaywrightInstallScriptAsync(string installTargetPath, Action<string>? onStatusChange = null)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var psScript = Path.Combine(appDir, "playwright.ps1");
            var shScript = Path.Combine(appDir, "playwright.sh");

            _logger.Information("Running bundled Playwright installer targeting {Path}...", installTargetPath);

            bool VerifyInstall(string sourceLabel)
            {
                var ok = HasChromiumInstalled(installTargetPath);
                if (ok)
                {
                    _logger.Information("{Source} installer finished and Chromium is present at {Path}", sourceLabel, installTargetPath);
                    return true;
                }

                _logger.Warning("{Source} installer finished but Chromium is still missing at {Path}", sourceLabel, installTargetPath);
                return false;
            }

            async Task<bool> TryRunAsync(string exe, string args, string sourceLabel, string statusMessage)
            {
                onStatusChange?.Invoke(statusMessage);
                if (!await RunProcessAsync(exe, args, appDir, installTargetPath, onStatusChange, TimeSpan.FromMinutes(5)))
                {
                    return false;
                }

                return VerifyInstall(sourceLabel);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (File.Exists(psScript))
                {
                    // Prefer pwsh (64-bit) then fallback to Windows PowerShell
                    if (await TryRunAsync("pwsh", $"-NoProfile -NonInteractive -File \"{psScript}\" install chromium", "pwsh", "Đang cài đặt bằng PowerShell Core..."))
                    {
                        return true;
                    }

                    _logger.Information("pwsh failed or not found, trying Windows PowerShell...");
                    var candidates = GetWindowsPowerShellCandidates().ToList();
                    _logger.Information("Found {Count} PowerShell candidates: {Candidates}", candidates.Count, string.Join(", ", candidates));

                    foreach (var powershellPath in candidates)
                    {
                        _logger.Information("Trying PowerShell at: {Path}", powershellPath);
                        if (await TryRunAsync(powershellPath, $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{psScript}\" install chromium", powershellPath, "Đang cài đặt bằng Windows PowerShell..."))
                        {
                            return true;
                        }
                    }

                    return false;
                }
                return false;
            }

            // macOS/Linux: prefer bash (no pwsh dependency), then pwsh as fallback
            if (File.Exists(shScript))
            {
                if (await TryRunAsync("bash", $"\"{shScript}\" install chromium", "bash", "Đang cài đặt bằng bash script..."))
                {
                    return true;
                }
            }

            if (File.Exists(psScript))
            {
                return await TryRunAsync("pwsh", $"-NoProfile -NonInteractive -File \"{psScript}\" install chromium", "pwsh", "Đang cài đặt bằng PowerShell...");
            }

            return false;
        }

        private IEnumerable<string> GetWindowsPowerShellCandidates()
        {
            // Ensure we try 64-bit PowerShell even when the app is 32-bit (use SysNative).
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
            var candidates = new List<string>();

            // For 64-bit process or native 64-bit system, use System32
            var system32 = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(system32))
            {
                candidates.Add(system32);
            }

            // For 32-bit process on 64-bit system, use SysNative to access 64-bit PowerShell
            var sysNative = Path.Combine(systemRoot, "SysNative", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(sysNative))
            {
                candidates.Add(sysNative);
            }

            // Syswow64 is the 32-bit version on 64-bit systems - ONLY use as last resort
            var sysWow64 = Path.Combine(systemRoot, "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe");

            // PATH fallback
            candidates.Add("powershell");

            // Add 32-bit version only as final fallback (will likely fail)
            if (File.Exists(sysWow64))
            {
                candidates.Add(sysWow64);
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<bool> RunProcessAsync(string fileName, string arguments, string workingDir, string? browserPath = null, Action<string>? onStatusChange = null, TimeSpan? timeout = null)
        {
            try
            {
                timeout ??= TimeSpan.FromMinutes(5);

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
                var waitTask = process.WaitForExitAsync();
                var combined = Task.WhenAll(stdoutTask, stderrTask, waitTask);

                static string SafeRead(Task<string> task)
                {
                    try
                    {
                        return task.IsCompleted ? task.Result : string.Empty;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }

                var completed = await Task.WhenAny(combined, Task.Delay(timeout.Value));
                if (completed != combined)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // ignore kill failures
                    }

                    await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(TimeSpan.FromSeconds(5)));
                    var stdoutTimeout = SafeRead(stdoutTask);
                    var stderrTimeout = SafeRead(stderrTask);
                    if (!string.IsNullOrWhiteSpace(stdoutTimeout))
                    {
                        _logger.Information("playwright install stdout (timeout): {Stdout}", stdoutTimeout);
                    }
                    if (!string.IsNullOrWhiteSpace(stderrTimeout))
                    {
                        _logger.Warning("playwright install stderr (timeout): {Stderr}", stderrTimeout);
                    }

                    _logger.Warning("playwright install via {Exe} timed out after {Timeout}.", fileName, timeout);
                    onStatusChange?.Invoke("Quá thời gian cài đặt. Đang chuyển sang cách khác...");
                    return false;
                }

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
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2 /* ERROR_FILE_NOT_FOUND */)
            {
                // Executable not found (e.g., pwsh missing) – treat as a soft failure so caller can try another runner.
                _logger.Warning("{Exe} not found when attempting playwright install: {Message}", fileName, ex.Message);
                onStatusChange?.Invoke($"{fileName} không khả dụng. Đang thử trình khác...");
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
