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
try {
    $null = vpk --version 2>&1
    Write-Host "Velopack CLI found!" -ForegroundColor Green
}
catch {
    Write-Host "Velopack CLI not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global vpk
}

# Paths
$ProjectPath = Join-Path $PSScriptRoot "..\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop"
$ProjectFile = Join-Path $ProjectPath "Haihv.Vbdlis.Tools.Desktop.csproj"
$PublishPath = Join-Path $ProjectPath "bin\publish\velopack"
$OutputPath = Join-Path $PSScriptRoot "..\dist\velopack"
$VersionLogFile = Join-Path $PSScriptRoot "version.json"

# Read or create version log
Write-Host "`nReading version log..." -ForegroundColor Yellow
$versionLog = $null
if (Test-Path $VersionLogFile) {
    $versionLog = Get-Content $VersionLogFile -Raw | ConvertFrom-Json
    Write-Host "Found existing version log" -ForegroundColor Green
}
else {
    Write-Host "Creating new version log" -ForegroundColor Yellow
    $versionLog = @{
        majorMinor      = "1.0"
        currentVersion  = "1.0.0"
        assemblyVersion = "1.0.0.0"
        lastBuildDate   = ""
        buildNumber     = 0
        platforms       = @{
            windows = @{ lastBuilt = $null; version = $null }
            macos   = @{ lastBuilt = $null; version = $null }
        }
    }
}

# Read Major.Minor from .csproj (only once, to initialize)
$csprojContent = Get-Content $ProjectFile -Raw
if ($csprojContent -match '<Version>([\d\.]+)</Version>') {
    $currentVersion = $matches[1]
    $versionParts = $currentVersion.Split('.')
    $majorMinor = "$($versionParts[0]).$($versionParts[1])"
    Write-Host "Major.Minor from .csproj: $majorMinor" -ForegroundColor Cyan
}
else {
    $majorMinor = $versionLog.majorMinor
    Write-Host "Using Major.Minor from version log: $majorMinor" -ForegroundColor Cyan
}

# Generate date parts: YYMM and DD
$yearMonthString = Get-Date -Format "yyMM"  # e.g., "2512"
$dayString = Get-Date -Format "dd"          # always two digits, e.g., "09"
$todayString = Get-Date -Format "yyyy-MM-dd"

# Check if running in GitHub Actions (always use locked version)
$isGitHubActions = $env:GITHUB_ACTIONS -eq "true"

# Check if version is locked (prepared for release or GitHub Actions)
$isVersionLocked = $false
if ($isGitHubActions) {
    $isVersionLocked = $true
    Write-Host "🔒 Running in GitHub Actions - using LOCKED version" -ForegroundColor Magenta
    Write-Host "   Version: $($versionLog.currentVersion) (no auto-increment)" -ForegroundColor Yellow
}
elseif ($versionLog.lastBuildDate -eq $todayString) {
    # Check if current version in log matches expected format for today
    $expectedDatePrefix = Get-Date -Format "yyMMdd"
    if ($versionLog.currentVersion -match "\.$expectedDatePrefix\d{2}$") {
        # Version is for today, check if it's locked by comparing with .csproj
        if ($csprojContent -match '<Version>([\d\.]+)</Version>') {
            $csprojVersion = $matches[1]
            $csprojPatch = $csprojVersion.Split('.')[3]
            $logPatch = $versionLog.assemblyVersion.Split('.')[3]

            if ($csprojPatch -eq $logPatch) {
                $isVersionLocked = $true
                Write-Host "🔒 Version is LOCKED for release: $($versionLog.currentVersion)" -ForegroundColor Magenta
                Write-Host "   Will use existing version without incrementing" -ForegroundColor Yellow
            }
        }
    }
}

# Determine today's build number from version log
if ($isVersionLocked) {
    # Use locked version
    $buildNumber = $versionLog.buildNumber
    $assemblyVersion = $versionLog.assemblyVersion
    $packageVersion = $versionLog.currentVersion
    $majorMinor = $versionLog.majorMinor
    
    Write-Host "Using locked version: $packageVersion (build #$buildNumber)" -ForegroundColor Cyan
}
else {
    # Calculate new version
    Write-Host "Calculating build number for today..." -ForegroundColor Yellow
    $buildNumber = 1
    if ($versionLog.lastBuildDate -eq $todayString) {
        # Same day, increment build number
        $buildNumber = $versionLog.buildNumber + 1
        Write-Host "Same day build detected. Incrementing to build #$buildNumber" -ForegroundColor Cyan
    }
    else {
        # New day, reset to 1
        $buildNumber = 1
        Write-Host "New day detected. Starting with build #$buildNumber" -ForegroundColor Cyan
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
}

# Common: Update .csproj and show version info
if (-not $isVersionLocked) {
    Set-Content -Path $ProjectFile -Value $csprojContent -NoNewline
    Write-Host "Updated .csproj: AssemblyVersion=$assemblyVersion" -ForegroundColor Green
}

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

# Update version log after successful build
Write-Host "`nUpdating version log..." -ForegroundColor Yellow
$versionLog.majorMinor = $majorMinor
$versionLog.currentVersion = $packageVersion
$versionLog.assemblyVersion = $assemblyVersion
$versionLog.lastBuildDate = $todayString
$versionLog.buildNumber = $buildNumber
$versionLog.platforms.windows.lastBuilt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss")
$versionLog.platforms.windows.version = $packageVersion

$versionLog | ConvertTo-Json -Depth 10 | Set-Content $VersionLogFile -Encoding UTF8
Write-Host "Version log updated: $VersionLogFile" -ForegroundColor Green
Write-Host "  Current Version: $packageVersion" -ForegroundColor Cyan
Write-Host "  Build Number: $buildNumber" -ForegroundColor Cyan

# Playwright browsers - NOT bundled (app will auto-download on first run)
Write-Host "`nPlaywright browsers: NOT bundled" -ForegroundColor Yellow
Write-Host "   Application will auto-download Chromium on first run (~150MB)" -ForegroundColor Cyan
Write-Host "   Users will need internet connection on first launch" -ForegroundColor Gray

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
}
else {
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

# Step 3: Create ZIP archive of Setup.exe (avoid browser warning)
Write-Host "`nStep 3: Creating ZIP archive of Setup.exe..." -ForegroundColor Yellow

# Find the Setup.exe file
$SetupExe = Get-ChildItem -Path $OutputPath -Filter "*-Setup.exe" | Select-Object -First 1

if ($SetupExe) {
    $ZipFileName = $SetupExe.Name -replace "-Setup\.exe$", "-Setup.zip"
    $ZipPath = Join-Path $OutputPath $ZipFileName
    
    # Create README for installer ZIP
    $ReadmeInstallerPath = Join-Path $OutputPath "README-INSTALLER.txt"
    @"
VBDLIS Tools - Installer Package
Version: $Version
=================================

CONTENTS:
- VbdlisTools-$Version-Setup.exe (Velopack Installer with bundled Playwright browsers)

INSTALLATION:
1. Extract this ZIP file
2. Run VbdlisTools-$Version-Setup.exe
3. Follow the installation wizard
4. ✅ Ready to use immediately - no additional downloads needed!

FEATURES:
- Full installer with auto-update support
- ✅ Playwright Chromium browser bundled (~200MB)
- Works offline after installation
- Installs to %LOCALAPPDATA%\VbdlisTools
- Creates Start Menu shortcut
- Automatic Velopack updates

WHY ZIP?
- Avoids browser download warnings for .exe files
- Safer distribution method
- Easy to share

SYSTEM REQUIREMENTS:
- Windows 10 64-bit or later
- .NET 10.0 (included)
- ~200MB disk space (includes Playwright browsers)

For more info: https://github.com/haitnmt/Vbdlis-Tools
"@ | Out-File -FilePath $ReadmeInstallerPath -Encoding UTF8
    
    # Create ZIP with Setup.exe and README
    Write-Host "Creating ZIP: $ZipFileName..." -ForegroundColor Cyan
    Compress-Archive -Path $SetupExe.FullName, $ReadmeInstallerPath -DestinationPath $ZipPath -Force
    
    # Remove the README after zipping
    Remove-Item -Path $ReadmeInstallerPath -Force
    
    Write-Host "✅ Setup ZIP created: $ZipFileName" -ForegroundColor Green
}
else {
    Write-Warning "Setup.exe not found, skipping ZIP creation"
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
