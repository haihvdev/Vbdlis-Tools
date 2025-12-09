# PowerShell script to create a new release tag and trigger GitHub Actions

param(
    [string]$Version = "",
    [string]$Message = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== VBDLIS Tools - Create Release ===" -ForegroundColor Green
Write-Host ""

# Read current version from version.json
$ScriptDir = $PSScriptRoot
$VersionFile = Join-Path $ScriptDir "build\version.json"

if (-not (Test-Path $VersionFile)) {
    Write-Host "❌ version.json not found!" -ForegroundColor Red
    exit 1
}

$VersionContent = Get-Content $VersionFile -Raw | ConvertFrom-Json
$CurrentVersion = $VersionContent.currentVersion

Write-Host "📦 Current version: $CurrentVersion" -ForegroundColor Cyan
Write-Host ""

# Determine version to use
if ([string]::IsNullOrEmpty($Version)) {
    $UseVersion = Read-Host "Use this version for release? (y/n/custom)"
    
    if ($UseVersion -eq "n") {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 0
    }
    elseif ($UseVersion -eq "custom") {
        $Version = Read-Host "Enter custom version (e.g., 1.0.25120906)"
    }
    else {
        $Version = $CurrentVersion
    }
}
else {
    Write-Host "Using provided version: $Version" -ForegroundColor Cyan
}

$TagName = "v$Version"

Write-Host ""
Write-Host "🏷️  Creating release tag: $TagName" -ForegroundColor Green
Write-Host ""

# Check if tag already exists
$TagExists = $false
try {
    git rev-parse $TagName 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $TagExists = $true
    }
}
catch {
    $TagExists = $false
}

if ($TagExists) {
    Write-Host "⚠️  Tag $TagName already exists!" -ForegroundColor Yellow
    $DeleteChoice = Read-Host "Delete and recreate? (y/n)"
    if ($DeleteChoice -eq "y") {
        git tag -d $TagName
        git push origin ":refs/tags/$TagName" 2>&1 | Out-Null
        Write-Host "✅ Old tag deleted" -ForegroundColor Green
    }
    else {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 0
    }
}

# Get release notes
if ([string]::IsNullOrEmpty($Message)) {
    Write-Host "📝 Enter release message:" -ForegroundColor Yellow
    $Message = Read-Host
    if ([string]::IsNullOrEmpty($Message)) {
        $Message = "Release $Version"
    }
}

# Check for uncommitted changes
$Status = git status --porcelain
if ($Status) {
    Write-Host ""
    Write-Host "📋 Uncommitted changes detected." -ForegroundColor Yellow
    $CommitChoice = Read-Host "Commit changes? (y/n)"
    if ($CommitChoice -eq "y") {
        $CommitMsg = Read-Host "Commit message"
        git add .
        git commit -m $CommitMsg
        Write-Host "✅ Changes committed" -ForegroundColor Green
    }
}

# Push commits
Write-Host ""
Write-Host "⬆️  Pushing commits to origin..." -ForegroundColor Yellow
$CurrentBranch = git rev-parse --abbrev-ref HEAD
git push origin $CurrentBranch

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to push commits" -ForegroundColor Red
    exit 1
}

# Create annotated tag
Write-Host ""
Write-Host "🏷️  Creating tag $TagName..." -ForegroundColor Yellow
git tag -a $TagName -m $Message

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to create tag" -ForegroundColor Red
    exit 1
}

# Push tag
Write-Host ""
Write-Host "⬆️  Pushing tag to origin..." -ForegroundColor Yellow
git push origin $TagName

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to push tag" -ForegroundColor Red
    exit 1
}

# Get repo URL
$RepoUrl = git config --get remote.origin.url
$RepoPath = $RepoUrl -replace '.*github.com[:/](.*?)(.git)?$', '$1'

Write-Host ""
Write-Host "✅ Release tag created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📺 GitHub Actions will now:" -ForegroundColor Cyan
Write-Host "   1. Build Windows (Velopack)" -ForegroundColor White
Write-Host "   2. Build macOS arm64 (Apple Silicon)" -ForegroundColor White
Write-Host "   3. Build macOS x64 (Intel)" -ForegroundColor White
Write-Host "   4. Create GitHub Release with all artifacts" -ForegroundColor White
Write-Host ""
Write-Host "🔗 Check progress at:" -ForegroundColor Cyan
Write-Host "   https://github.com/$RepoPath/actions" -ForegroundColor White
Write-Host ""
Write-Host "⏱️  Build will take approximately 10-15 minutes" -ForegroundColor Yellow
Write-Host ""
Write-Host "🎉 Done!" -ForegroundColor Green
