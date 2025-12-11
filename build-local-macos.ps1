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
try {
    $null = vpk --version 2>&1
    Write-Host "Velopack CLI found!" -ForegroundColor Green
}
catch {
    Write-Host "Velopack CLI not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global vpk
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

# Step 3: Create DMG from .app bundle
Write-Host "`nStep 3: Creating DMG installer..." -ForegroundColor Yellow

# Find the .app bundle created by Velopack
$AppBundle = Get-ChildItem -Path $OutputPath -Filter "*.app" -Directory | Select-Object -First 1

if ($AppBundle) {
    $DmgName = "VbdlisTools-$packageVersion-osx-$Arch.dmg"
    $DmgPath = Join-Path $OutputPath $DmgName
    $TempDmg = Join-Path $OutputPath "temp.dmg"
    $TempDir = Join-Path $OutputPath "dmg_temp"
    
    Write-Host "Found app bundle: $($AppBundle.Name)" -ForegroundColor Cyan
    
    # Create temporary directory for DMG contents
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    
    # Copy app bundle to temp directory
    Copy-Item -Path $AppBundle.FullName -Destination $TempDir -Recurse
    
    # Create Applications symlink (macOS specific - requires running on macOS)
    $ApplicationsLink = Join-Path $TempDir "Applications"
    if (-not (Test-Path $ApplicationsLink)) {
        # This will only work on macOS
        & ln -s /Applications $ApplicationsLink 2>$null
    }
    
    # Create comprehensive README
    $ReadmeContent = @'
VBDLIS Tools - macOS (Unsigned)
================================

⚠️ "App is damaged and can't be opened" ERROR?

This is NORMAL for unsigned apps. Choose one of these methods:

────────────────────────────────────────────────────────────
METHOD 1: Terminal Command (RECOMMENDED - Easiest)
────────────────────────────────────────────────────────────

1. Drag VBDLIS Tools.app to Applications folder
2. Open Terminal (Applications → Utilities → Terminal)
3. Copy and paste this command:

   xattr -cr "/Applications/VBDLIS Tools.app"

4. Press Enter
5. Now open VBDLIS Tools normally (double-click or Spotlight)

✅ Done! The app will open without any issues.

────────────────────────────────────────────────────────────
METHOD 2: Right-Click (Alternative)
────────────────────────────────────────────────────────────

1. Drag VBDLIS Tools.app to Applications folder
2. DON'T double-click the app yet
3. Right-click (or Control+Click) on VBDLIS Tools.app
4. Select "Open" from the menu
5. Click "Open" in the security dialog
6. App will open and macOS will remember this choice

✅ Done! You can now open the app normally in the future.

────────────────────────────────────────────────────────────
WHY THIS HAPPENS?
────────────────────────────────────────────────────────────

• This app is NOT signed with an Apple Developer certificate ($99/year)
• macOS Gatekeeper blocks unsigned apps downloaded from internet
• This is a FREE open-source app, so we don't have Apple Developer signing
• The commands above safely bypass this security check
• Your app and data are safe - this is just a macOS security feature

────────────────────────────────────────────────────────────
FEATURES
────────────────────────────────────────────────────────────

✅ Auto-update via Velopack
   - App checks for updates on startup
   - Download updates from GitHub Releases
   - No need to re-download DMG manually

✅ Native Apple Silicon support
   - Optimized for M1/M2/M3/M4 chips
   - Fast and efficient

────────────────────────────────────────────────────────────
SYSTEM REQUIREMENTS
────────────────────────────────────────────────────────────

• macOS 10.15 (Catalina) or later
• Apple Silicon (M1/M2/M3/M4) for arm64 build
• .NET 10.0 runtime (included in app)
• Internet connection (for auto-updates)

────────────────────────────────────────────────────────────
TROUBLESHOOTING
────────────────────────────────────────────────────────────

Q: Command not working?
A: Make sure you copied the FULL command including quotes

Q: Still see error after xattr command?
A: Try Method 2 (right-click → Open)

Q: App crashes on startup?
A: Check logs at ~/Library/Logs/VbdlisTools/

────────────────────────────────────────────────────────────
MORE INFORMATION
────────────────────────────────────────────────────────────

GitHub: https://github.com/haitnmt/Vbdlis-Tools
Issues: https://github.com/haitnmt/Vbdlis-Tools/issues

────────────────────────────────────────────────────────────

Enjoy using VBDLIS Tools! 🎉
'@
    
    $ReadmeContent | Out-File -FilePath (Join-Path $TempDir "README.txt") -Encoding UTF8
    
    # Create temporary DMG (requires hdiutil - macOS only)
    Write-Host "Creating temporary DMG..." -ForegroundColor Cyan
    
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed to create DMG, falling back to ZIP..."
        # Fallback: Create ZIP of app bundle
        $ZipName = "VbdlisTools-$packageVersion-osx-$Arch.zip"
        $ZipPath = Join-Path $OutputPath $ZipName
        Compress-Archive -Path $AppBundle.FullName -DestinationPath $ZipPath -Force
        Write-Host "✅ ZIP archive created: $ZipName" -ForegroundColor Green
    }
    else {
        # Convert to compressed DMG
        Write-Host "Compressing DMG..." -ForegroundColor Cyan
        & hdiutil convert $TempDmg -format UDZO -o $DmgPath 2>&1 | Out-Null
        if (Test-Path $TempDmg) {
            Remove-Item -Path $TempDmg -Force
        }
        
        Write-Host "✅ DMG created: $DmgName" -ForegroundColor Green
    }
    
    # Clean up temp directory
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force
    }
}
else {
    Write-Warning "App bundle not found in Velopack output"
    Write-Host "Available files:" -ForegroundColor Yellow
    Get-ChildItem -Path $OutputPath | Format-Table Name, Length
}

# Step 4: Create portable ZIP
Write-Host "`nStep 4: Creating portable ZIP..." -ForegroundColor Yellow
$PortableZip = Get-ChildItem -Path $OutputPath -Filter "*-Portable.zip" | Select-Object -First 1
if ($PortableZip) {
    Write-Host "✅ Portable ZIP already created by Velopack: $($PortableZip.Name)" -ForegroundColor Green
}

# List generated files
Write-Host "`n=== BUILD COMPLETED ===" -ForegroundColor Green
Write-Host "`nGenerated files:" -ForegroundColor Cyan
Get-ChildItem -Path $OutputPath -File | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}

Write-Host "`n✅ LOCAL BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "`nVersion built: $packageVersion" -ForegroundColor Cyan
Write-Host "Architecture: $Arch" -ForegroundColor Cyan
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host "`n📦 DISTRIBUTION FILES:" -ForegroundColor Yellow
Write-Host "   DMG:      VbdlisTools-$packageVersion-osx-$Arch.dmg" -ForegroundColor White
Write-Host "   Portable: Haihv.Vbdlis.Tools.Desktop-osx-Portable.zip" -ForegroundColor White
Write-Host "`n🚀 RECOMMENDED FOR USERS:" -ForegroundColor Yellow
Write-Host "   Share the DMG file - easiest to install!" -ForegroundColor White
Write-Host "   User just needs to run: xattr -cr `"/Applications/VBDLIS Tools.app`"" -ForegroundColor White
Write-Host ""
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "                    NEXT STEPS" -ForegroundColor Green
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 This is a LOCAL build for testing." -ForegroundColor Yellow
Write-Host ""
Write-Host "To create a RELEASE on GitHub:" -ForegroundColor Cyan
Write-Host "   ./create-release-macos.sh" -ForegroundColor White
Write-Host "   or" -ForegroundColor DarkGray
Write-Host "   .\create-release.ps1" -ForegroundColor White
Write-Host ""
Write-Host "This will:" -ForegroundColor Cyan
Write-Host "   1. Create git tag v$packageVersion" -ForegroundColor White
Write-Host "   2. Push to GitHub" -ForegroundColor White
Write-Host "   3. Trigger GitHub Actions to build Windows version" -ForegroundColor White
Write-Host "   4. You can manually upload macOS DMG with:" -ForegroundColor White
Write-Host "      gh release upload v$packageVersion $OutputPath/VbdlisTools-$packageVersion-osx-$Arch.dmg" -ForegroundColor DarkGray
Write-Host ""
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
