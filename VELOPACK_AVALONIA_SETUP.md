# Tích hợp Velopack Auto-Update vào Avalonia App

Hướng dẫn này dựa trên [official Avalonia sample](https://github.com/velopack/velopack/tree/develop/samples/CSharpAvalonia) của Velopack.

## Bước 1: Cài .NET 9.0 ASP.NET Core Runtime (Yêu cầu cho vpk tool)

```powershell
# Download và cài từ:
# https://dotnet.microsoft.com/download/dotnet/9.0
# Chọn: ASP.NET Core Runtime 9.0.x - Windows x64
```

## Bước 2: Cài Velopack CLI

```powershell
dotnet tool install --global vpk

# Verify
vpk --version
# Should show: 0.0.1298 or newer
```

## Bước 3: Thêm Velopack NuGet Package

```powershell
cd src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop
dotnet add package Velopack
```

## Bước 4: Modify Program.cs

Tìm file `Program.cs` và sửa `Main()` method:

**TRƯỚC:**
```csharp
[STAThread]
public static void Main(string[] args)
{
    BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
}
```

**SAU:**
```csharp
using Velopack;

[STAThread]
public static void Main(string[] args)
{
    // QUAN TRỌNG: Phải đặt TRƯỚC BuildAvaloniaApp()
    VelopackApp.Build().Run();

    BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
}
```

**Giải thích:**
- `VelopackApp.Build().Run()` xử lý các hook events của Velopack
- Phải gọi **TRƯỚC** Avalonia initialization
- Không ảnh hưởng đến Avalonia designer

## Bước 5: Thêm Auto-Update Service

Tạo file mới: `Services/UpdateService.cs`

```csharp
using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services;

public class UpdateService
{
    // Thay đổi URL này theo deployment của bạn
    private readonly string _updateUrl;
    private readonly UpdateManager? _updateManager;

    public UpdateService(string updateUrl = "")
    {
        // Mặc định dùng network share
        _updateUrl = string.IsNullOrEmpty(updateUrl)
            ? @"\\file\Setups\vbdlis-tools"
            : updateUrl;

        try
        {
            // SimpleWebSource hỗ trợ cả HTTP và UNC paths
            var source = new SimpleWebSource(_updateUrl);
            _updateManager = new UpdateManager(source);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize UpdateManager. Auto-update disabled.");
            _updateManager = null;
        }
    }

    /// <summary>
    /// Check for updates và download nếu có
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (_updateManager == null)
        {
            Log.Information("UpdateManager not initialized. Skipping update check.");
            return null;
        }

        try
        {
            Log.Information("Checking for updates from {UpdateUrl}...", _updateUrl);

            var updateInfo = await _updateManager.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                Log.Information("No updates available");
                return null;
            }

            Log.Information("Update available: {Version}", updateInfo.TargetFullRelease.Version);
            return updateInfo;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates");
            return null;
        }
    }

    /// <summary>
    /// Download update package
    /// </summary>
    public async Task<bool> DownloadUpdatesAsync(UpdateInfo updateInfo)
    {
        if (_updateManager == null || updateInfo == null)
            return false;

        try
        {
            Log.Information("Downloading update {Version}...", updateInfo.TargetFullRelease.Version);

            await _updateManager.DownloadUpdatesAsync(updateInfo);

            Log.Information("Update downloaded successfully");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download updates");
            return false;
        }
    }

    /// <summary>
    /// Apply updates và restart app
    /// </summary>
    public void ApplyUpdatesAndRestart(UpdateInfo? updateInfo = null)
    {
        if (_updateManager == null)
            return;

        try
        {
            Log.Information("Applying updates and restarting...");

            if (updateInfo != null)
            {
                _updateManager.ApplyUpdatesAndRestart(updateInfo);
            }
            else
            {
                _updateManager.ApplyUpdatesAndExit(null);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply updates");
        }
    }

    /// <summary>
    /// Kiểm tra nếu app đang chạy từ Velopack installer
    /// </summary>
    public bool IsInstalled => VelopackApp.IsFirstRun == false;
}
```

## Bước 6: Tích hợp Auto-Update vào App.axaml.cs

Sửa file `App.axaml.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Existing code...
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            // Check for updates after startup (non-blocking)
            _ = CheckForUpdatesAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task CheckForUpdatesAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            // Wait 5 seconds after startup
            await Task.Delay(5000);

            var updateService = new UpdateService();

            // Skip if not installed via Velopack
            if (!updateService.IsInstalled)
            {
                Log.Information("App not installed via Velopack, skipping update check");
                return;
            }

            // Check for updates
            var updateInfo = await updateService.CheckForUpdatesAsync();

            if (updateInfo != null)
            {
                // Download updates in background
                var downloaded = await updateService.DownloadUpdatesAsync(updateInfo);

                if (downloaded)
                {
                    // Show notification to user on UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ShowUpdateNotification(updateService, updateInfo, desktop);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Update check failed");
        }
    }

    private void ShowUpdateNotification(UpdateService updateService, UpdateInfo updateInfo, IClassicDesktopStyleApplicationLifetime desktop)
    {
        // TODO: Replace with proper Avalonia dialog
        // For now, just log
        Log.Information("Update ready. Restart to apply version {Version}", updateInfo.TargetFullRelease.Version);

        // Optionally show dialog:
        // var result = await MessageBox.Show(
        //     desktop.MainWindow,
        //     $"Phiên bản mới {updateInfo.TargetFullRelease.Version} đã sẵn sàng.\n\nBạn có muốn restart để cập nhật không?",
        //     "Cập nhật mới",
        //     MessageBox.MessageBoxButtons.YesNo
        // );
        //
        // if (result == MessageBox.MessageBoxResult.Yes)
        // {
        //     updateService.ApplyUpdatesAndRestart(updateInfo);
        // }
    }
}
```

## Bước 7: Build với Velopack

```powershell
# Build installer
.\build\windows-velopack.ps1 -Version "1.0.4"
```

**Output:**
- `dist/velopack/VbdlisTools-1.0.4-win-Setup.exe` - Installer
- `dist/velopack/VbdlisTools-1.0.4-win-full.nupkg` - Package
- `dist/velopack/RELEASES` - Manifest

## Bước 8: Deploy

### Option A: Network Share

```powershell
# Copy lên network share
Copy-Item "dist\velopack\*" "\\file\Setups\vbdlis-tools" -Recurse -Force

# Users install từ
# \\file\Setups\vbdlis-tools\VbdlisTools-1.0.4-win-Setup.exe
```

### Option B: Web Server

```powershell
# Copy lên web server
Copy-Item "dist\velopack\*" "C:\inetpub\wwwroot\vbdlis-tools" -Recurse -Force

# Users download từ
# https://your-server.com/vbdlis-tools/VbdlisTools-1.0.4-win-Setup.exe
```

## Bước 9: Test Auto-Update

### Test 1: Install version 1.0.4

```powershell
# Run installer
.\dist\velopack\VbdlisTools-1.0.4-win-Setup.exe

# App install vào: %LOCALAPPDATA%\VbdlisTools\
# Shortcut tạo trong Start Menu
```

### Test 2: Build version 1.0.5

```powershell
# 1. Update version trong .csproj
# <Version>1.0.5</Version>

# 2. Build version mới
.\build\windows-velopack.ps1 -Version "1.0.5"

# 3. Copy lên cùng vị trí (ghi đè)
Copy-Item "dist\velopack\*" "\\file\Setups\vbdlis-tools" -Recurse -Force
```

### Test 3: Verify Auto-Update

```
1. Mở app version 1.0.4
2. Đợi 5 giây (update check delay)
3. Check logs: App sẽ log "Update available: 1.0.5"
4. App tự download update in background
5. Hiển thị notification (nếu đã implement dialog)
6. Restart app để apply update
```

## Configuration Options

### Thay đổi Update URL:

```csharp
// Trong App.axaml.cs hoặc config file
var updateService = new UpdateService("https://your-server.com/vbdlis-tools/");
// Hoặc network share
var updateService = new UpdateService(@"\\server\share\vbdlis-tools");
```

### Thay đổi Update Check Interval:

```csharp
// Check sau 10 giây thay vì 5 giây
await Task.Delay(10000);
```

### Check Updates On-Demand:

```csharp
// Thêm button "Check for Updates" trong UI
private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
{
    var updateService = new UpdateService();
    var updateInfo = await updateService.CheckForUpdatesAsync();

    if (updateInfo != null)
    {
        var downloaded = await updateService.DownloadUpdatesAsync(updateInfo);
        if (downloaded)
        {
            // Show dialog and restart
            updateService.ApplyUpdatesAndRestart(updateInfo);
        }
    }
    else
    {
        // Show "No updates available"
    }
}
```

## Delta Updates

Velopack tự động tạo delta packages:

```
Version 1.0.4 → 1.0.5:
- Full download: ~100MB
- Delta download: ~5-20MB (chỉ tải phần thay đổi)
```

Không cần config gì thêm, Velopack tự động xử lý!

## Troubleshooting

### Lỗi: vpk needs .NET 9.0

```powershell
# Download và cài:
https://dotnet.microsoft.com/download/dotnet/9.0
# Chọn: ASP.NET Core Runtime 9.0.x
```

### App không check updates

```csharp
// Check if installed via Velopack
if (VelopackApp.IsFirstRun)
{
    // First run after install - update check sẽ chạy lần sau
}

// Check logs
Log.Information("UpdateService initialized with URL: {Url}", _updateUrl);
```

### Updates không được apply

```csharp
// Verify RELEASES file exists
var releasesPath = Path.Combine(_updateUrl, "RELEASES");
if (File.Exists(releasesPath))
{
    Log.Information("RELEASES file found");
}
```

## So sánh: Network Share vs Velopack

| Feature | Network Share | Velopack |
|---------|---------------|----------|
| Setup | ✅ Rất dễ | ⭐ Trung bình |
| Dependencies | .NET 10 only | .NET 9 + 10 |
| Auto-update | ❌ Manual | ✅ Tự động |
| Delta updates | ❌ Không | ✅ Có |
| User experience | Manual copy | Click & auto-update |
| Bandwidth | Full download | Delta only |

## Khuyến nghị

**Bắt đầu với Network Share** (đơn giản):
```powershell
.\build\windows-simple.ps1
```

**Sau này nâng cấp lên Velopack** khi cần auto-update:
1. Cài .NET 9.0 Runtime
2. Thêm Velopack code (steps above)
3. Build với `windows-velopack.ps1`
4. Deploy và test

---

Xem thêm:
- [Official Avalonia Sample](https://github.com/velopack/velopack/tree/develop/samples/CSharpAvalonia)
- [Velopack Docs](https://docs.velopack.io/)
- [BUILD_DEPLOY.md](BUILD_DEPLOY.md) - Hướng dẫn triển khai chi tiết
