# Script to build and package Windows installer
# Requires:
#   - .NET 10.0 SDK
#   - Inno Setup 6.0+ (optional, for creating setup.exe)

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [switch]$CreateSetup = $false,
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building VBDLIS Tools for Windows ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan

# Paths
$ProjectPath = Join-Path $PSScriptRoot "..\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop"
$PublishPath = Join-Path $ProjectPath "bin\publish\win-x64"
$OutputPath = Join-Path $PSScriptRoot "..\dist\windows"

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    Remove-Item -Path $PublishPath -Recurse -Force
}
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Build and publish
Write-Host "`nPublishing application..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $PublishPath `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Remove Playwright browsers (but keep driver files needed for installation)
Write-Host "`nRemoving Playwright browsers from output (will be downloaded on first run)..." -ForegroundColor Yellow
$PlaywrightPath = Join-Path $PublishPath ".playwright"
if (Test-Path $PlaywrightPath) {
    # Remove only browser binaries, keep .playwright/node and .playwright/package
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "chromium-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "firefox-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "webkit-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Get-ChildItem -Path $PlaywrightPath -Directory -Filter "ffmpeg-*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Write-Host "Removed Playwright browser binaries (kept driver files)" -ForegroundColor Green
}

# Copy to dist folder
Write-Host "`nCopying to dist folder..." -ForegroundColor Yellow
Copy-Item -Path "$PublishPath\*" -Destination $OutputPath -Recurse -Force

# Create ZIP package
Write-Host "`nCreating ZIP package..." -ForegroundColor Yellow
$ZipFileName = "VbdlisTools-Windows-x64-v$Version.zip"
$ZipPath = Join-Path (Split-Path $OutputPath -Parent) $ZipFileName

if (Test-Path $ZipPath) {
    Remove-Item -Path $ZipPath -Force
}

Compress-Archive -Path "$OutputPath\*" -DestinationPath $ZipPath -CompressionLevel Optimal

# Create setup.exe with Inno Setup (if requested and available)
$SetupExePath = $null
if ($CreateSetup) {
    if (Test-Path $InnoSetupPath) {
        Write-Host "`n=== Creating Setup.exe with Inno Setup ===" -ForegroundColor Green

        # Update version in installer script
        $InstallerScript = Join-Path $PSScriptRoot "installer.iss"
        $TempScript = Join-Path $PSScriptRoot "installer_temp.iss"

        # Read and update version
        $content = Get-Content $InstallerScript -Raw
        $content = $content -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""

        # Write temp script
        $content | Set-Content $TempScript -Encoding UTF8

        # Compile with Inno Setup
        Write-Host "Compiling installer..." -ForegroundColor Yellow
        & $InnoSetupPath $TempScript /Q

        # Clean up temp script
        Remove-Item $TempScript -Force

        # Check if setup was created
        $SetupExePath = Join-Path (Split-Path $OutputPath -Parent) "VbdlisTools-Setup-v$Version.exe"
        if (Test-Path $SetupExePath) {
            Write-Host "Setup.exe created successfully!" -ForegroundColor Green
            Write-Host "Setup file: $SetupExePath" -ForegroundColor Cyan
        } else {
            Write-Host "Failed to create setup.exe" -ForegroundColor Red
        }
    } else {
        Write-Host "`nInno Setup not found at: $InnoSetupPath" -ForegroundColor Yellow
        Write-Host "Skipping setup.exe creation. Install from: https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
Write-Host "Output folder: $OutputPath" -ForegroundColor Cyan
Write-Host "ZIP package: $ZipPath" -ForegroundColor Cyan
if ($SetupExePath -and (Test-Path $SetupExePath)) {
    Write-Host "Setup.exe: $SetupExePath" -ForegroundColor Cyan
}
Write-Host "`nNote: Playwright browsers are NOT included. They will be downloaded automatically on first run." -ForegroundColor Yellow
