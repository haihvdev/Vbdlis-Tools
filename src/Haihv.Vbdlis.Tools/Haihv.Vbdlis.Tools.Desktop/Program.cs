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
    }
}
