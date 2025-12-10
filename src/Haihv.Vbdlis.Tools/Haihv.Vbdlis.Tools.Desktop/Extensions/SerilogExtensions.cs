using System;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;

namespace Haihv.Vbdlis.Tools.Desktop.Extensions;

public static class SerilogExtensions
{
    public static void ConfigureSerilog()
    {
        var logDirectory = GetLogDirectory();
        var logFilePath = Path.Combine(logDirectory, "vbdlis-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                path: logFilePath,
                restrictedToMinimumLevel: LogEventLevel.Information,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Application starting...");
        Log.Information("Log directory: {LogDirectory}", logDirectory);
    }

    private static string GetLogDirectory()
    {
        string baseFolder;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else // Linux
        {
            baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");
        }

        var logFolder = Path.Combine(baseFolder, "Haihv.Vbdlis.Tools", "Logs");
        Directory.CreateDirectory(logFolder);
        return logFolder;
    }
}
