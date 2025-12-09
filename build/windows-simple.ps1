# Script to build simple deployment for Network Share
# No dependencies, just publish and ZIP
# Perfect for internal network deployment without auto-update complexity

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.4"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building VBDLIS Tools for Network Share Deployment ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan

# Paths
$ProjectPath = Join-Path $PSScriptRoot "..\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop"
$ProjectFile = Join-Path $ProjectPath "Haihv.Vbdlis.Tools.Desktop.csproj"
$PublishPath = Join-Path $ProjectPath "bin\publish\network-share"
$OutputPath = Join-Path $PSScriptRoot "..\dist\network-share"

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    Remove-Item -Path $PublishPath -Recurse -Force
}
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Publish application
Write-Host "`nPublishing application..." -ForegroundColor Yellow
dotnet publish $ProjectFile `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $PublishPath `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Remove Playwright browsers
Write-Host "`nRemoving Playwright browsers..." -ForegroundColor Yellow
$PlaywrightPath = Join-Path $PublishPath ".playwright"
if (Test-Path $PlaywrightPath) {
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "chromium-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "firefox-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "webkit-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "ffmpeg-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
}

# Copy to output folder
Write-Host "`nCopying to output folder..." -ForegroundColor Yellow
Copy-Item -Path "$PublishPath\*" -Destination $OutputPath -Recurse -Force

# Create README
$ReadmePath = Join-Path $OutputPath "README.txt"
$ReadmeContent = @"
VBDLIS Tools - Network Share Deployment
Version: $Version
========================================

DEPLOYMENT:
-----------
1. Copy toàn bộ folder này lên network share:
   Ví dụ: \\server\Setups\VbdlisTools\

2. Share folder với users:
   - Right-click folder > Properties > Sharing > Advanced Sharing
   - Tích "Share this folder"
   - Permissions: Give "Everyone" Read access

3. Users chạy từ network share:
   \\server\Setups\VbdlisTools\Haihv.Vbdlis.Tools.Desktop.exe

HOẶC: XCOPY DEPLOYMENT
----------------------
Users có thể copy folder về máy local:

1. Copy toàn bộ folder về máy:
   xcopy /E /I "\\server\Setups\VbdlisTools" "C:\Apps\VbdlisTools\"

2. Tạo shortcut:
   - Right-click Desktop > New > Shortcut
   - Location: C:\Apps\VbdlisTools\Haihv.Vbdlis.Tools.Desktop.exe
   - Name: VBDLIS Tools

3. Chạy từ shortcut

UPDATE:
-------
Để update lên version mới:

1. Build version mới:
   .\build\windows-simple.ps1 -Version "1.0.5"

2. Copy files mới lên network share (ghi đè):
   xcopy /Y /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"

3. Users chạy version mới (không cần uninstall)

LƯU Ý:
-------
- Playwright browsers (~300MB) sẽ tự động tải khi chạy lần đầu
- Lưu vào: %LOCALAPPDATA%\ms-playwright
- Không cần quyền Administrator
- Mỗi user chỉ cần tải Playwright 1 lần

TROUBLESHOOTING:
----------------
Nếu gặp lỗi ".NET Runtime not found":
- Download .NET 10.0 Desktop Runtime:
  https://dotnet.microsoft.com/download/dotnet/10.0

Nếu Windows Defender block:
- Right-click exe > Properties > Unblock
- Hoặc add exclusion trong Windows Security

For more deployment options, see BUILD_DEPLOY.md
"@

Set-Content -Path $ReadmePath -Value $ReadmeContent -Encoding UTF8

# Create install script for users
$InstallScriptPath = Join-Path $OutputPath "Install-ToLocal.ps1"
$InstallScript = @"
# Script để users copy app về máy local
param(
    [string]`$InstallPath = "C:\Apps\VbdlisTools"
)

Write-Host "Installing VBDLIS Tools to `$InstallPath..." -ForegroundColor Green

# Create directory
New-Item -ItemType Directory -Path `$InstallPath -Force | Out-Null

# Copy files
Write-Host "Copying files..." -ForegroundColor Yellow
Copy-Item -Path "`$PSScriptRoot\*" -Destination `$InstallPath -Recurse -Force -Exclude "Install-ToLocal.ps1","README.txt"

# Create desktop shortcut
Write-Host "Creating desktop shortcut..." -ForegroundColor Yellow
`$WshShell = New-Object -ComObject WScript.Shell
`$Shortcut = `$WshShell.CreateShortcut("`$env:USERPROFILE\Desktop\VBDLIS Tools.lnk")
`$Shortcut.TargetPath = "`$InstallPath\Haihv.Vbdlis.Tools.Desktop.exe"
`$Shortcut.WorkingDirectory = `$InstallPath
`$Shortcut.Description = "VBDLIS Tools"
`$Shortcut.Save()

Write-Host "`nInstallation completed!" -ForegroundColor Green
Write-Host "Application installed to: `$InstallPath" -ForegroundColor Cyan
Write-Host "Desktop shortcut created" -ForegroundColor Cyan
Write-Host "`nYou can now run VBDLIS Tools from the desktop shortcut." -ForegroundColor Yellow
"@

Set-Content -Path $InstallScriptPath -Value $InstallScript -Encoding UTF8

Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
Write-Host "Output folder: $OutputPath" -ForegroundColor Cyan
Write-Host "`nTo deploy:" -ForegroundColor Yellow
Write-Host "1. Copy entire folder to network share:" -ForegroundColor White
Write-Host "   xcopy /E /I `"$OutputPath`" `"\\server\Setups\VbdlisTools\`"" -ForegroundColor Gray
Write-Host "`n2. Users can either:" -ForegroundColor White
Write-Host "   - Run from network: \\server\Setups\VbdlisTools\Haihv.Vbdlis.Tools.Desktop.exe" -ForegroundColor Gray
Write-Host "   - Or install local: Right-click Install-ToLocal.ps1 > Run with PowerShell" -ForegroundColor Gray
Write-Host "`nNote: No auto-update. For new versions, recopy to network share." -ForegroundColor Yellow
