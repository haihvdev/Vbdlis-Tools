# Script to build MSIX package for modern .NET apps
# MSIX provides auto-update, easy deployment like ClickOnce
# Requires: .NET 10.0 SDK, Windows SDK 10.0.19041.0+

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.4.0",
    [string]$Publisher = "CN=vpdkbacninh.vn",
    [string]$PackageName = "VbdlisTools",
    [switch]$Sign = $false,
    [string]$CertificatePath = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building VBDLIS Tools MSIX Package ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan
Write-Host "Publisher: $Publisher" -ForegroundColor Cyan

# Paths
$ProjectPath = Join-Path $PSScriptRoot "..\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop"
$ProjectFile = Join-Path $ProjectPath "Haihv.Vbdlis.Tools.Desktop.csproj"
$PublishPath = Join-Path $ProjectPath "bin\publish\msix"
$OutputPath = Join-Path $PSScriptRoot "..\dist\msix"

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    Remove-Item -Path $PublishPath -Recurse -Force
}
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Step 1: Publish the application
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

# Step 2: Create MSIX manifest
Write-Host "`nCreating MSIX manifest..." -ForegroundColor Yellow

$ManifestPath = Join-Path $PublishPath "AppxManifest.xml"
$IconPath = "Assets\appicon.png" # You may need to create this

$ManifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Identity Name="$PackageName"
            Publisher="$Publisher"
            Version="$Version" />
  <Properties>
    <DisplayName>VBDLIS Tools</DisplayName>
    <PublisherDisplayName>vpdkbacninh.vn</PublisherDisplayName>
    <Logo>$IconPath</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Resources>
    <Resource Language="vi-VN" />
    <Resource Language="en-US" />
  </Resources>
  <Applications>
    <Application Id="VbdlisTools" Executable="Haihv.Vbdlis.Tools.Desktop.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="VBDLIS Tools"
                          Description="Công cụ hỗ trợ làm việc với hệ thống VBDLIS"
                          BackgroundColor="transparent"
                          Square150x150Logo="$IconPath"
                          Square44x44Logo="$IconPath">
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

Set-Content -Path $ManifestPath -Value $ManifestContent -Encoding UTF8

# Step 3: Find MakeAppx.exe
Write-Host "`nSearching for MakeAppx.exe..." -ForegroundColor Yellow
$MakeAppxPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
)

$MakeAppx = $null
foreach ($path in $MakeAppxPaths) {
    if (Test-Path $path) {
        $MakeAppx = $path
        break
    }
}

if (-not $MakeAppx) {
    Write-Host "ERROR: MakeAppx.exe not found!" -ForegroundColor Red
    Write-Host "Please install Windows SDK: https://developer.microsoft.com/windows/downloads/windows-sdk/" -ForegroundColor Yellow
    Write-Host "`nAlternatively, you can use this manual command:" -ForegroundColor Yellow
    Write-Host "makeappx.exe pack /d `"$PublishPath`" /p `"$OutputPath\$PackageName-$Version.msix`"" -ForegroundColor Cyan
    exit 1
}

Write-Host "Found MakeAppx: $MakeAppx" -ForegroundColor Green

# Step 4: Create MSIX package
Write-Host "`nCreating MSIX package..." -ForegroundColor Yellow
$MsixFile = Join-Path $OutputPath "$PackageName-$Version.msix"

& $MakeAppx pack /d $PublishPath /p $MsixFile /nv

if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX packaging failed!" -ForegroundColor Red
    exit 1
}

# Step 5: Sign MSIX (if certificate provided)
if ($Sign -and $CertificatePath) {
    Write-Host "`nSigning MSIX package..." -ForegroundColor Yellow

    # Find SignTool.exe
    $SignToolPaths = @(
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe",
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe",
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
    )

    $SignTool = $null
    foreach ($path in $SignToolPaths) {
        if (Test-Path $path) {
            $SignTool = $path
            break
        }
    }

    if ($SignTool) {
        if ($CertificatePassword) {
            & $SignTool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $MsixFile
        } else {
            & $SignTool sign /fd SHA256 /f $CertificatePath $MsixFile
        }
        Write-Host "MSIX signed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Warning: SignTool.exe not found. Package is unsigned." -ForegroundColor Yellow
    }
} else {
    Write-Host "`nWarning: Package is unsigned. Users will need to trust the certificate manually." -ForegroundColor Yellow
    Write-Host "To sign, use: -Sign -CertificatePath `"cert.pfx`" -CertificatePassword `"password`"" -ForegroundColor Cyan
}

# Create README
$ReadmePath = Join-Path $OutputPath "README.txt"
$ReadmeContent = @"
VBDLIS Tools - MSIX Package
Version: $Version
========================

INSTALLATION:
------------
1. Double-click the .msix file
2. Click "Install" when prompted
3. Application installs to: C:\Program Files\WindowsApps\

REQUIREMENTS:
------------
- Windows 10 version 1809 (build 17763) or later
- .NET 10.0 Runtime (auto-installed if missing)

CERTIFICATE TRUST:
-----------------
If the package is unsigned or uses a self-signed certificate:

1. Right-click the .msix file > Properties > Digital Signatures
2. Select the certificate > Details > View Certificate
3. Install Certificate > Local Machine > Place in "Trusted Root Certification Authorities"

Or use PowerShell:
Add-AppxPackage -Path "$PackageName-$Version.msix"

AUTO-UPDATE:
-----------
To enable auto-update, publish to Microsoft Store or use App Installer.

UNINSTALL:
---------
Settings > Apps > VBDLIS Tools > Uninstall

For more info: https://docs.microsoft.com/windows/msix/
"@

Set-Content -Path $ReadmePath -Value $ReadmeContent -Encoding UTF8

Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
Write-Host "MSIX package: $MsixFile" -ForegroundColor Cyan
Write-Host "Size: $([Math]::Round((Get-Item $MsixFile).Length / 1MB, 2)) MB" -ForegroundColor Cyan
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Sign the package (if not already signed)" -ForegroundColor White
Write-Host "2. Test installation: Add-AppxPackage -Path `"$MsixFile`"" -ForegroundColor White
Write-Host "3. Distribute to users or publish to Microsoft Store" -ForegroundColor White
