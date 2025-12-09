# Script to build Velopack installer (successor to Squirrel.Windows)
# Velopack provides ClickOnce-like auto-update for modern .NET apps
# Requires: .NET 10.0 SDK, Velopack CLI

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.4",
    [string]$UpdateUrl = "",  # URL where releases will be hosted
    [string]$IconPath = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building VBDLIS Tools with Velopack ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan

# Check if Velopack is installed
Write-Host "`nChecking for Velopack CLI..." -ForegroundColor Yellow
$velopackInstalled = $false
try {
    $null = vpk --version 2>&1
    $velopackInstalled = $true
    Write-Host "Velopack CLI found!" -ForegroundColor Green
} catch {
    Write-Host "Velopack CLI not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global vpk
    $velopackInstalled = $true
}

# Paths
$ProjectPath = Join-Path $PSScriptRoot "..\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop"
$ProjectFile = Join-Path $ProjectPath "Haihv.Vbdlis.Tools.Desktop.csproj"
$PublishPath = Join-Path $ProjectPath "bin\publish\velopack"
$OutputPath = Join-Path $PSScriptRoot "..\dist\velopack"

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    Remove-Item -Path $PublishPath -Recurse -Force
}
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Step 1: Publish application
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

# Step 2: Create Velopack release
Write-Host "`nCreating Velopack release..." -ForegroundColor Yellow

$VpkArgs = @(
    "pack"
    "--packId", "VbdlisTools"
    "--packVersion", $Version
    "--packDir", $PublishPath
    "--mainExe", "Haihv.Vbdlis.Tools.Desktop.exe"
    "--outputDir", $OutputPath
    "--packTitle", "VBDLIS Tools"
    "--packAuthors", "vpdkbacninh.vn"
)

if ($IconPath -and (Test-Path $IconPath)) {
    $VpkArgs += "--icon", $IconPath
}

if ($UpdateUrl) {
    Write-Host "Update URL: $UpdateUrl" -ForegroundColor Cyan
    # UpdateUrl will be configured in app code
}

Write-Host "Running: vpk $($VpkArgs -join ' ')" -ForegroundColor Gray
& vpk @VpkArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Velopack packaging failed!" -ForegroundColor Red
    Write-Host "Make sure vpk is installed: dotnet tool install --global vpk" -ForegroundColor Yellow
    exit 1
}

# Create deployment guide
$ReadmePath = Join-Path $OutputPath "README.txt"
$ReadmeContent = @"
VBDLIS Tools - Velopack Installer
Version: $Version
==================================

OUTPUT FILES:
------------
  - VbdlisTools-$Version-win-Setup.exe  - Installer cho người dùng mới
  - VbdlisTools-$Version-win-full.nupkg - Full package
  - RELEASES                            - Update manifest

DEPLOYMENT:
----------
1. For NEW users:
   - Distribute VbdlisTools-$Version-win-Setup.exe
   - Users run Setup.exe to install

2. For AUTO-UPDATE:
   - Upload all files to web server or network share
   - URL example: https://your-server.com/vbdlis-tools/
   - Or network: \\server\share\vbdlis-tools\

3. Update URL Configuration:
   - Add Velopack NuGet package to your project:
     dotnet add package Velopack

   - Add update code to your app:

     using Velopack;

     public async Task CheckForUpdates()
     {
         var mgr = new UpdateManager("https://your-server.com/vbdlis-tools/");
         var newVersion = await mgr.CheckForUpdatesAsync();
         if (newVersion != null)
         {
             await mgr.DownloadUpdatesAsync(newVersion);
             mgr.ApplyUpdatesAndRestart(newVersion);
         }
     }

INSTALLATION:
------------
- Installs to: %LOCALAPPDATA%\VbdlisTools\
- Creates Start Menu shortcut
- No admin rights required

AUTO-UPDATE:
-----------
- App checks for updates on startup
- Downloads delta updates (only changed files)
- Updates in background
- Restart to apply updates

PUBLISHING NEW VERSION:
----------------------
1. Build new version:
   .\build\build-squirrel.ps1 -Version "1.0.5"

2. Upload new files:
   - Upload all files to same location
   - Velopack creates delta packages automatically
   - Users auto-update on next launch

UNINSTALL:
---------
- Settings > Apps > VBDLIS Tools > Uninstall

For more info: https://docs.velopack.io/
"@

Set-Content -Path $ReadmePath -Value $ReadmeContent -Encoding UTF8

Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
Write-Host "Output folder: $OutputPath" -ForegroundColor Cyan
Write-Host "Setup file: $OutputPath\VbdlisTools-$Version-win-Setup.exe" -ForegroundColor Cyan
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Test installation: Run VbdlisTools-$Version-win-Setup.exe" -ForegroundColor White
Write-Host "2. Upload files to web server or network share for auto-update" -ForegroundColor White
if ($UpdateUrl) {
    Write-Host "3. Ensure UpdateUrl in code points to: $UpdateUrl" -ForegroundColor White
}
Write-Host "`nNote: Add 'Velopack' NuGet package to enable auto-update in your app" -ForegroundColor Yellow
