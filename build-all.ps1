# Quick script to build and package for all platforms
# Run this before creating a GitHub release to test locally

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building VBDLIS Tools for All Platforms ===" -ForegroundColor Green
Write-Host ""

$ScriptDir = $PSScriptRoot

# Build Windows
Write-Host "🪟 Building Windows..." -ForegroundColor Cyan
& "$ScriptDir\build\windows-velopack.ps1" -Configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Windows build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Windows build completed!" -ForegroundColor Green
Write-Host ""

# Note for macOS
Write-Host "🍎 macOS build requires a Mac machine" -ForegroundColor Yellow
Write-Host "   Run on Mac: ./build/macos.sh $Configuration both" -ForegroundColor White
Write-Host ""

# Summary
Write-Host "=== Build Summary ===" -ForegroundColor Green
Write-Host ""
Write-Host "Windows artifacts:" -ForegroundColor Cyan
Get-ChildItem "$ScriptDir\dist\velopack" -File | ForEach-Object {
    Write-Host "  ✓ $($_.Name)" -ForegroundColor White
}

Write-Host ""
Write-Host "📦 Ready for release!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test the Windows installer locally" -ForegroundColor White
Write-Host "  2. Build macOS on a Mac (if needed)" -ForegroundColor White
Write-Host "  3. Run: .\create-release.ps1" -ForegroundColor White
Write-Host "     Or manually: git tag -a v1.0.x -m 'Release'; git push origin v1.0.x" -ForegroundColor White
