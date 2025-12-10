# Script to build locally with auto-incrementing version for macOS
# This script builds the application for LOCAL TESTING on macOS
# - Auto-increments version based on date and build number
# - Updates version.json with new version
# - Use this for development and testing
#
# For RELEASE builds, use: ./create-release.ps1

param(
    [string]$Configuration = "Release",
    [string]$Arch = "arm64"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building VBDLIS Tools LOCALLY with Velopack (macOS) ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Architecture: $Arch" -ForegroundColor Cyan
Write-Host "Build Mode: LOCAL (auto-increment version)" -ForegroundColor Yellow
Write-Host ""

# Check if Velopack is installed
Write-Host "Checking for Velopack CLI..." -ForegroundColor Yellow
$velopackInstalled = $false
try {
    $null = vpk --version 2>&1
    $velopackInstalled = $true
    Write-Host "Velopack CLI found!" -ForegroundColor Green
}
catch {
    Write-Host "Velopack CLI not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global vpk
    $velopackInstalled = $true
}

# Paths
$ProjectPath = Join-Path $PSScriptRoot "src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop"
$ProjectFile = Join-Path $ProjectPath "Haihv.Vbdlis.Tools.Desktop.csproj"
$PublishPath = Join-Path $ProjectPath "bin/publish/velopack"
$OutputPath = Join-Path $PSScriptRoot "dist/velopack"
$VersionLogFile = Join-Path $PSScriptRoot "build/version.json"

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
        currentVersion  = ""
        assemblyVersion = ""
        lastBuildDate   = ""
        buildNumber     = 0
        platforms       = @{
            windows = @{
                lastBuilt = ""
                version   = ""
            }
            macos   = @{
                lastBuilt = ""
                version   = ""
            }
        }
    }
}

# Read Major.Minor from version log or .csproj
$majorMinor = $versionLog.majorMinor
if ([string]::IsNullOrEmpty($majorMinor)) {
    Write-Host "Reading Major.Minor from .csproj..." -ForegroundColor Yellow
    $csprojContent = Get-Content $ProjectFile -Raw
    if ($csprojContent -match '<Version>([\d\.]+)</Version>') {
        $existingVersion = $matches[1]
        $parts = $existingVersion.Split('.')
        $majorMinor = "$($parts[0]).$($parts[1])"
    }
    else {
        $majorMinor = "1.0"
    }
    $versionLog.majorMinor = $majorMinor
}
Write-Host "Using Major.Minor: $majorMinor" -ForegroundColor Cyan

# Calculate version - ALWAYS INCREMENT for local builds
Write-Host "`nCalculating new version for LOCAL build..." -ForegroundColor Yellow
$todayString = [DateTime]::Now.ToString("yyyy-MM-dd")
$dateString = [DateTime]::Now.ToString("yyMMdd")
$yearMonth = [DateTime]::Now.ToString("yyMM")
$dayString = [DateTime]::Now.ToString("dd")

# Always increment build number for local builds
if ($versionLog.lastBuildDate -eq $todayString) {
    $buildNum = $versionLog.buildNumber + 1
    Write-Host "Same day build detected. Incrementing to build #$buildNum" -ForegroundColor Cyan
}
else {
    $buildNum = 1
    Write-Host "New day detected. Starting with build #$buildNum" -ForegroundColor Cyan
}

$buildNumString = $buildNum.ToString("00")

# Create two different version formats
# 1. Assembly version (4-part): Major.Minor.YYMM.DDBB
$dayBuild = "$dayString$buildNumString"
$assemblyVersion = "$majorMinor.$yearMonth.$dayBuild"

# 2. Package version (3-part SemVer2): Major.Minor.YYMMDDBB
$patchNumber = "$dateString$buildNumString"
$packageVersion = "$majorMinor.$patchNumber"

Write-Host "`n=== VERSION CALCULATED ===" -ForegroundColor Green
Write-Host "Assembly Version: $assemblyVersion (4-part for .NET)" -ForegroundColor Cyan
Write-Host "Package Version:  $packageVersion (3-part SemVer2 for Velopack)" -ForegroundColor Cyan
Write-Host "Build Number:     $buildNum" -ForegroundColor Cyan
Write-Host "Date:             $todayString" -ForegroundColor Cyan
Write-Host ""

# Update version log
$versionLog.currentVersion = $packageVersion
$versionLog.assemblyVersion = $assemblyVersion
$versionLog.lastBuildDate = $todayString
$versionLog.buildNumber = $buildNum

# Update platform-specific info
if (-not $versionLog.platforms) {
    $versionLog | Add-Member -MemberType NoteProperty -Name "platforms" -Value @{
        windows = @{ lastBuilt = ""; version = "" }
        macos   = @{ lastBuilt = ""; version = "" }
    }
}
if (-not $versionLog.platforms.macos) {
    $versionLog.platforms | Add-Member -MemberType NoteProperty -Name "macos" -Value @{ lastBuilt = ""; version = "" }
}

$versionLog.platforms.macos.lastBuilt = [DateTime]::Now.ToString("yyyy-MM-ddTHH:mm:ss")
$versionLog.platforms.macos.version = $packageVersion

# Save version log
Write-Host "Updating version log..." -ForegroundColor Yellow
$versionLog | ConvertTo-Json -Depth 10 | Set-Content $VersionLogFile -Encoding UTF8
Write-Host "Version log updated!" -ForegroundColor Green

# Update .csproj with assembly version
Write-Host "`nUpdating .csproj with new version..." -ForegroundColor Yellow
$csprojContent = Get-Content $ProjectFile -Raw

# Update or add version properties
if ($csprojContent -match '<AssemblyVersion>.*</AssemblyVersion>') {
    $csprojContent = $csprojContent -replace '<AssemblyVersion>.*</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
}
else {
    $csprojContent = $csprojContent -replace '(<PropertyGroup>)', "`$1`n    <AssemblyVersion>$assemblyVersion</AssemblyVersion>"
}

if ($csprojContent -match '<FileVersion>.*</FileVersion>') {
    $csprojContent = $csprojContent -replace '<FileVersion>.*</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>"
}
else {
    $csprojContent = $csprojContent -replace '(<PropertyGroup>)', "`$1`n    <FileVersion>$assemblyVersion</FileVersion>"
}

if ($csprojContent -match '<Version>.*</Version>') {
    $csprojContent = $csprojContent -replace '<Version>.*</Version>', "<Version>$assemblyVersion</Version>"
}
else {
    $csprojContent = $csprojContent -replace '(<PropertyGroup>)', "`$1`n    <Version>$assemblyVersion</Version>"
}

# Add InformationalVersion for Velopack (3-part version)
if ($csprojContent -match '<InformationalVersion>.*</InformationalVersion>') {
    $csprojContent = $csprojContent -replace '<InformationalVersion>.*</InformationalVersion>', "<InformationalVersion>$packageVersion</InformationalVersion>"
}
else {
    $csprojContent = $csprojContent -replace '(<Version>.*</Version>)', "`$1`n    <InformationalVersion>$packageVersion</InformationalVersion>"
}

Set-Content -Path $ProjectFile -Value $csprojContent -NoNewline
Write-Host ".csproj updated with version $assemblyVersion" -ForegroundColor Green

# Use packageVersion for Velopack
$Version = $packageVersion

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
Write-Host "`nStep 1: Publishing application..." -ForegroundColor Yellow
$runtime = "osx-$Arch"

dotnet publish $ProjectFile `
    --configuration $Configuration `
    --runtime $runtime `
    --self-contained true `
    --output $PublishPath `
    /p:PublishSingleFile=false `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=embedded

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
}

Write-Host "✅ Application published successfully!" -ForegroundColor Green

# Step 2: Create Velopack package
Write-Host "`nStep 2: Creating Velopack package..." -ForegroundColor Yellow

$VelopackArgs = @(
    "pack"
    "--packId", "Haihv.Vbdlis.Tools.Desktop"
    "--packVersion", $Version
    "--packDir", $PublishPath
    "--mainExe", "Haihv.Vbdlis.Tools.Desktop"
    "--outputDir", $OutputPath
    "--packTitle", "VBDLIS Tools"
    "--packAuthors", "haitnmt"
    "--icon", (Join-Path $ProjectPath "Assets/appicon.icns")
    "--runtime", $runtime
)

Write-Host "Running Velopack pack with version $Version..." -ForegroundColor Cyan
& vpk @VelopackArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Velopack packaging failed with exit code $LASTEXITCODE"
}

Write-Host "✅ Velopack package created!" -ForegroundColor Green

# Step 3: Create ZIP archive of the installer
Write-Host "`nStep 3: Creating ZIP archive of installer..." -ForegroundColor Yellow

# Find the installer file
$InstallerFile = Get-ChildItem -Path $OutputPath -Filter "*.pkg" -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $InstallerFile) {
    $InstallerFile = Get-ChildItem -Path $OutputPath -Filter "*Setup*" -ErrorAction SilentlyContinue | Select-Object -First 1
}

if ($InstallerFile) {
    $ZipFileName = $InstallerFile.BaseName + ".zip"
    $ZipPath = Join-Path $OutputPath $ZipFileName
    
    # Create README for installer ZIP
    $ReadmePath = Join-Path $OutputPath "README-INSTALLER.txt"
    @"
VBDLIS Tools - Installer Package (macOS)
Version: $packageVersion
Architecture: $Arch
==========================================

CONTENTS:
- $($InstallerFile.Name) (Velopack Installer)

INSTALLATION:
1. Extract this ZIP file
2. Run $($InstallerFile.Name)
3. Follow the installation wizard

FEATURES:
- Full installer with auto-update support
- Automatic Velopack updates from GitHub Releases
- Native Apple Silicon support

SYSTEM REQUIREMENTS:
- macOS 10.15 (Catalina) or later
- Apple Silicon (M1/M2/M3/M4) for arm64 build
- .NET 10.0 (included)

For more info: https://github.com/haitnmt/Vbdlis-Tools
"@ | Out-File -FilePath $ReadmePath -Encoding UTF8
    
    # Create ZIP with installer and README
    Write-Host "Creating ZIP: $ZipFileName..." -ForegroundColor Cyan
    Compress-Archive -Path $InstallerFile.FullName, $ReadmePath -DestinationPath $ZipPath -Force
    
    # Remove the README after zipping
    Remove-Item -Path $ReadmePath -Force
    
    Write-Host "✅ Installer ZIP created: $ZipFileName" -ForegroundColor Green
}
else {
    Write-Warning "Installer file not found, skipping ZIP creation"
}

# List generated files
Write-Host "`n=== BUILD COMPLETED ===" -ForegroundColor Green
Write-Host "`nGenerated files:" -ForegroundColor Cyan
Get-ChildItem -Path $OutputPath -File | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}

Write-Host "`n✅ LOCAL BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "`nVersion built: $packageVersion" -ForegroundColor Cyan
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host "`n📝 NOTE: This is a LOCAL build for testing." -ForegroundColor Yellow
Write-Host "   To create a RELEASE, use: ./create-release.ps1" -ForegroundColor Yellow
Write-Host ""
