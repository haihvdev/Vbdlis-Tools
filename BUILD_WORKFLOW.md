# Build & Release Workflow

## 📋 Overview

This project supports building on both **Windows** and **macOS** with automatic version management.

## 🛠️ Build Scripts

### Windows
- **`build-local.ps1`** - Build locally on Windows
- **`create-release.ps1`** - Create GitHub release

### macOS
- **`build-local-macos.sh`** - Build locally on macOS (Bash)
- **`build-local-macos.ps1`** - Build locally on macOS (PowerShell)
- **`create-release-macos.sh`** - Create GitHub release (Bash)
- **`create-release.ps1`** - Create GitHub release (PowerShell)

## 🔄 Workflow

### 1️⃣ Local Build (Auto-increment version)

**On Windows:**
```powershell
.\build-local.ps1
```

**On macOS (Bash):**
```bash
./build-local-macos.sh
```

**On macOS (PowerShell):**
```bash
pwsh build-local-macos.ps1
```

This will:
- ✅ Auto-increment version based on date + build number
- ✅ Update `build/version.json`
- ✅ Build application
- ✅ Create installer packages

**Windows Output:**
- `Haihv.Vbdlis.Tools.Desktop-<version>-win-Setup.exe`
- `Haihv.Vbdlis.Tools.Desktop-<version>-win-Setup.zip`

**macOS Output:**
- `VbdlisTools-<version>-osx-arm64.dmg` ⭐ **Recommended**
- `Haihv.Vbdlis.Tools.Desktop-osx-Portable.zip`
- `Haihv.Vbdlis.Tools.Desktop-osx-Setup.pkg` (unsigned, not recommended)

### 2️⃣ Create GitHub Release

**On Windows:**
```powershell
.\create-release.ps1
```

**On macOS:**
```bash
./create-release-macos.sh
# or
pwsh create-release.ps1
```

This will:
- ✅ Read version from `build/version.json` (LOCKED version)
- ✅ Create git tag `v<version>`
- ✅ Push to GitHub
- ✅ Trigger GitHub Actions to build **Windows version only**

### 3️⃣ Manual Upload macOS DMG

After GitHub Actions completes, manually upload macOS DMG:

```bash
gh release upload v<version> dist/velopack/VbdlisTools-<version>-osx-arm64.dmg
```

## 📦 Version Management

Version format: `Major.Minor.YYMMDDBB`

Example: `1.0.25121028`
- `1.0` = Major.Minor
- `251210` = Date (Dec 10, 2025)
- `28` = Build number (28th build on this date)

**Version is stored in:** `build/version.json`

```json
{
  "majorMinor": "1.0",
  "currentVersion": "1.0.25121028",
  "assemblyVersion": "1.0.2512.1028",
  "lastBuildDate": "2025-12-10",
  "buildNumber": 28,
  "platforms": {
    "windows": {
      "lastBuilt": "2025-12-10T19:43:56",
      "version": "1.0.25121023"
    },
    "macos": {
      "lastBuilt": "2025-12-10T20:41:24",
      "version": "1.0.25121028"
    }
  }
}
```

## ⚙️ GitHub Actions

When you push a tag (via `create-release.ps1` or `create-release-macos.sh`):

1. GitHub Actions reads version from `build/version.json`
2. Builds **Windows version ONLY**
3. Creates GitHub Release with Windows artifacts
4. macOS DMG must be uploaded manually

## 📝 Distribution Files

### Windows
✅ **Setup.exe** - Velopack installer with auto-update
✅ **Setup.zip** - ZIP archive of installer

### macOS
✅ **DMG** - Recommended for distribution
- Easy drag-and-drop installation
- Includes README with instructions
- User runs: `xattr -cr "/Applications/VBDLIS Tools.app"`

✅ **Portable ZIP** - No installation needed

❌ **PKG** - Not recommended (unsigned, will be blocked)

## 🚀 Quick Start

**For Development:**
```bash
# Build on your platform
.\build-local.ps1           # Windows
./build-local-macos.sh      # macOS

# Test the build
```

**For Release:**
```bash
# 1. Build locally first (generates version)
.\build-local.ps1           # Windows
./build-local-macos.sh      # macOS

# 2. Create release
.\create-release.ps1        # Any platform
./create-release-macos.sh   # macOS

# 3. Wait for GitHub Actions to build Windows

# 4. Upload macOS DMG manually (if built on macOS)
gh release upload v1.0.25121028 dist/velopack/VbdlisTools-1.0.25121028-osx-arm64.dmg
```

## 🔧 Prerequisites

**Windows:**
- .NET 10 SDK
- Velopack CLI (`dotnet tool install -g vpk`)

**macOS:**
- .NET 10 SDK
- .NET 9 Runtime (for Velopack CLI)
- Velopack CLI (`dotnet tool install -g vpk`)
- Homebrew (optional, for .NET installation)

**Install .NET 9 on macOS:**
```bash
brew install --cask dotnet-sdk@9
```

## 📖 Notes

- **Local builds** always auto-increment version
- **GitHub Actions** uses LOCKED version from `version.json`
- macOS builds are done **locally** (not on GitHub Actions)
- Only **Windows builds** run on GitHub Actions
- DMG file works on **any Mac** (unsigned but safe)

## 🐛 Troubleshooting

### macOS: "App is damaged and can't be opened"
This is normal for unsigned apps. Run:
```bash
xattr -cr "/Applications/VBDLIS Tools.app"
```

### Velopack CLI error: Framework 9.0.0 not found
Install .NET 9:
```bash
brew install --cask dotnet-sdk@9
```

### Version not incrementing
Make sure `build/version.json` exists and is readable.
