using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Haihv.Vbdlis.Tools.Desktop.Data;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services.Data;

/// <summary>
/// Service quản lý database sử dụng Entity Framework Core với SQLite
/// </summary>
public class DatabaseService : IDatabaseService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<DatabaseService>();
    private VbdlisDbContext? _dbContext;
    private readonly string _databasePath;
    private bool _disposed;

    public DatabaseService()
    {
        _databasePath = GetDefaultDatabasePath();
        _logger.Debug("DatabaseService initialized with path: {DatabasePath}", _databasePath);
    }

    /// <summary>
    /// Khởi tạo với custom database path
    /// </summary>
    public DatabaseService(string databasePath)
    {
        _databasePath = databasePath;
        _logger.Debug("DatabaseService initialized with custom path: {DatabasePath}", _databasePath);
    }

    /// <summary>
    /// Lấy database path mặc định theo platform
    /// </summary>
    private static string GetDefaultDatabasePath()
    {
        string baseFolder;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: %LOCALAPPDATA%\Haihv.Vbdlis.Tools\Data
            baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: ~/Library/Application Support/Haihv.Vbdlis.Tools/Data
            baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else
        {
            // Linux: ~/.local/share/Haihv.Vbdlis.Tools/Data
            baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");
        }

        var appFolder = Path.Combine(baseFolder, "Haihv.Vbdlis.Tools", "Data");
        Directory.CreateDirectory(appFolder);

        return Path.Combine(appFolder, "vbdlis.db");
    }

    /// <summary>
    /// Lấy DbContext instance (tạo mới nếu chưa có)
    /// </summary>
    public VbdlisDbContext GetDbContext()
    {
        if (_dbContext == null)
        {
            var optionsBuilder = new DbContextOptionsBuilder<VbdlisDbContext>();
            optionsBuilder.UseSqlite($"Data Source={_databasePath}");

            _dbContext = new VbdlisDbContext(optionsBuilder.Options);
            _logger.Debug("Created new DbContext instance");
        }

        return _dbContext;
    }

    /// <summary>
    /// Khởi tạo database và chạy migrations
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        try
        {
            _logger.Information("Initializing database...");
            var dbContext = GetDbContext();

            // Tạo database nếu chưa tồn tại và áp dụng migrations
            await dbContext.Database.EnsureCreatedAsync();
            _logger.Information("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize database");
            throw;
        }
    }

    /// <summary>
    /// Đóng database connection
    /// </summary>
    public void CloseDatabase()
    {
        if (_dbContext != null)
        {
            _dbContext.Dispose();
            _dbContext = null;
            _logger.Debug("Database connection closed");
        }
    }

    /// <summary>
    /// Lấy đường dẫn đến file database
    /// </summary>
    public string GetDatabasePath() => _databasePath;

    public void Dispose()
    {
        if (!_disposed)
        {
            CloseDatabase();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
