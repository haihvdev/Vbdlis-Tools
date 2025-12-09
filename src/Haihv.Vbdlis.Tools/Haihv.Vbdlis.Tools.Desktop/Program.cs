using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Haihv.Vbdlis.Tools.Desktop.Extensions;
using Serilog;
using Serilog.Events;
using Velopack;

namespace Haihv.Vbdlis.Tools.Desktop
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // CRITICAL: VelopackApp.Build().Run() must be called BEFORE any Avalonia initialization
            // This handles Velopack update lifecycle events (install, update, uninstall)
            VelopackApp.Build().Run();

            // macOS: Remove quarantine attribute on first run
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                RemoveMacOsQuarantine();
            }

            SerilogExtensions.ConfigureSerilog();
            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ứng dụng bị lỗi không mong muốn và phải đóng.");

            }
            finally
            {
                Log.CloseAndFlush();

            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        /// <summary>
        /// Remove macOS quarantine attribute to fix "app is damaged" error
        /// This runs automatically on first launch after download
        /// </summary>
        private static void RemoveMacOsQuarantine()
        {
            try
            {
                // Get the app bundle path
                var appPath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(appPath))
                    return;

                // Navigate up to .app bundle (from Contents/MacOS/executable to .app)
                var appBundlePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(appPath)));
                if (string.IsNullOrEmpty(appBundlePath) || !appBundlePath.EndsWith(".app"))
                    return;

                Log.Information("Attempting to remove macOS quarantine attribute from: {AppPath}", appBundlePath);

                // Run xattr command to remove quarantine
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xattr",
                    Arguments = $"-dr com.apple.quarantine \"{appBundlePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process != null)
                {
                    process.WaitForExit(5000); // 5 second timeout
                    if (process.ExitCode == 0)
                    {
                        Log.Information("Successfully removed quarantine attribute");
                    }
                    else
                    {
                        var error = process.StandardError.ReadToEnd();
                        Log.Warning("Failed to remove quarantine attribute: {Error}", error);
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't crash the app if this fails - it's a nice-to-have feature
                Log.Warning(ex, "Could not remove macOS quarantine attribute (this is not critical)");
            }
        }


    }
}
