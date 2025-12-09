# Script to build Velopack installer (successor to Squirrel.Windows)
# Velopack provides ClickOnce-like auto-update for modern .NET apps
# Requires: .NET 10.0 SDK, Velopack CLI
#
# Version format: Major.Minor.YYMMDDBB
# Example: 1.0.25110901 (version 1.0, built on 2025-11-09, 1st build of the day)
# - Major.Minor: Read from .csproj (e.g., 1.0)
# - YYMMDDBB: Date + Build number as single number (25110901 = 2025-11-09, build 01)
# Note: Uses 3-part SemVer2 format (Major.Minor.Patch) required by Velopack

param(
    [string]$Configuration = "Release",
    [string]$UpdateUrl = ""  # URL where releases will be hosted
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building VBDLIS Tools with Velopack ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

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

# Read current version info from .csproj
Write-Host "`nReading version from .csproj..." -ForegroundColor Yellow
$csprojContent = Get-Content $ProjectFile -Raw
$versionParts = @()
if ($csprojContent -match '<Version>([\d\.]+)</Version>') {
    $currentVersion = $matches[1]
    # Extract Major.Minor (first two parts)
    $versionParts = $currentVersion.Split('.')
    $majorMinor = "$($versionParts[0]).$($versionParts[1])"
    Write-Host "Current Major.Minor version: $majorMinor" -ForegroundColor Cyan
} else {
    Write-Host "Could not read version from .csproj, using default 1.0" -ForegroundColor Yellow
    $majorMinor = "1.0"
}

# Generate date parts: YYMM and DD
$yearMonthString = Get-Date -Format "yyMM"  # e.g., "2512"
$yearMonth = [int]$yearMonthString          # still <= 65535
$dayString = Get-Date -Format "dd"          # always two digits, e.g., "09"

# Determine today's build number based on previous .csproj version
Write-Host "Calculating build number for today..." -ForegroundColor Yellow
$buildNumber = 1
if ($versionParts.Count -ge 4) {
    $previousYearMonth = $versionParts[2]
    $previousDayBuild = $versionParts[3].PadLeft(4, '0')
    $previousDay = $previousDayBuild.Substring(0, 2)
    $previousBuild = $previousDayBuild.Substring(2, 2)

    if (($previousYearMonth -eq $yearMonthString) -and ($previousDay -eq $dayString)) {
        $buildNumber = ([int]$previousBuild) + 1
    }
}

# Create two different version formats:
# 1. Assembly version (4-part): Major.Minor.YYMM.DDBB
#    All parts <= 65535: Major=1, Minor=0, YYMM=2512, DDBB=0901 (day 09, build 01)
$buildNumberString = $buildNumber.ToString("00")
$dayBuildString = "$dayString$buildNumberString"
$assemblyVersion = "$majorMinor.$yearMonthString.$dayBuildString"

# 2. Package version (3-part SemVer2): Major.Minor.YYMMDDBB
$dateString = Get-Date -Format "yyMMdd"
$patchNumber = "$dateString$buildNumberString"
$packageVersion = "$majorMinor.$patchNumber"

Write-Host "Auto-generated versions:" -ForegroundColor Green
Write-Host "  Assembly: $assemblyVersion (for .NET - 4 parts, each <= 65535)" -ForegroundColor Gray
Write-Host "  Package:  $packageVersion (for Velopack - 3-part SemVer2)" -ForegroundColor Gray
Write-Host "  Date: YYMM=$yearMonthString, DD=$dayString, Build #$buildNumber" -ForegroundColor Gray

# Update version in .csproj (use 4-part assembly version)
Write-Host "`nUpdating version in .csproj to $assemblyVersion..." -ForegroundColor Yellow
$csprojContent = $csprojContent -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>"
$csprojContent = $csprojContent -replace '<Version>[\d\.]+</Version>', "<Version>$assemblyVersion</Version>"
Set-Content -Path $ProjectFile -Value $csprojContent -NoNewline
Write-Host "Updated .csproj: AssemblyVersion=$assemblyVersion" -ForegroundColor Green

# Use packageVersion for Velopack
$Version = $packageVersion

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    Remove-Item -Path $PublishPath -Recurse -Force
}
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Step 1: Publish application
Write-Host "`nPublishing application..." -ForegroundColor Yellow
# Use assemblyVersion for build (not packageVersion)
dotnet publish $ProjectFile `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $PublishPath `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:Version=$assemblyVersion

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

# Find icon file (.ico in Assets folder)
$ProjectAssetsPath = Join-Path $ProjectPath "Assets"
$IconPath = Get-ChildItem -Path $ProjectAssetsPath -Filter "*.ico" -ErrorAction SilentlyContinue | Select-Object -First 1 | ForEach-Object { $_.FullName }

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
    Write-Host "Using icon: $IconPath" -ForegroundColor Green
    $VpkArgs += "--icon", $IconPath
} else {
    Write-Host "Warning: No .ico file found in Assets folder. Setup will use default icon." -ForegroundColor Yellow
    Write-Host "Tip: Copy your .ico file to $ProjectAssetsPath" -ForegroundColor Yellow
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
   .\build\windows-velopack.ps1
   (Version is auto-generated: Major.Minor.YYMMDDBB)

2. Upload new files:
   - Upload all files to same location
   - Velopack creates delta packages automatically
   - Users auto-update on next launch

VERSION FORMAT:
--------------
Major.Minor.YYMMDDBB (3-part SemVer2)
- Major.Minor: From .csproj (e.g., 1.0)
- YYMMDDBB: Patch number combining date + build (e.g., 25110901)
  - YYMMDD: Date (251109 = 2025-11-09)
  - BB: Build number (01, 02, 03...)

Example: 1.0.25110901 = Version 1.0, built on 2025-11-09, 1st build

UNINSTALL:
---------
- Settings > Apps > VBDLIS Tools > Uninstall

For more info: https://docs.velopack.io/
"@

Set-Content -Path $ReadmePath -Value $ReadmeContent -Encoding UTF8

Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Cyan
Write-Host "Output folder: $OutputPath" -ForegroundColor Cyan
Write-Host "Setup file: $OutputPath\VbdlisTools-$Version-win-Setup.exe" -ForegroundColor Cyan
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Test installation: Run VbdlisTools-$Version-win-Setup.exe" -ForegroundColor White
Write-Host "2. Upload files to web server or network share for auto-update" -ForegroundColor White
if ($UpdateUrl) {
    Write-Host "3. Ensure UpdateUrl in code points to: $UpdateUrl" -ForegroundColor White
}
Write-Host "`nNote: To change Major.Minor version, edit the .csproj file" -ForegroundColor Yellow
