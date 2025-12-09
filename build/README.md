# Build Scripts

Thư mục này chứa các script build cho các nền tảng khác nhau.

## 📁 Cấu trúc

### Windows Scripts

| Script | Mô tả | Khuyến nghị |
|--------|-------|-------------|
| **windows-simple.ps1** | Build đơn giản cho network share/portable | ⭐ **Dùng cho triển khai nội bộ** |
| **windows-velopack.ps1** | Build với Velopack auto-update | Khi cần auto-update |
| **windows-innosetup.ps1** | Build Setup.exe với Inno Setup | Traditional installer |
| **windows-msix.ps1** | Build MSIX package | Microsoft Store |

### macOS Script

| Script | Mô tả |
|--------|-------|
| **macos.sh** | Build .app bundle và DMG cho macOS |

### Support Files

- **installer.iss** - Inno Setup configuration
- **.gitignore** - Git ignore rules

---

## 🚀 Quick Start

### Windows - Simple Deployment (Khuyến nghị)

```powershell
# Build
.\build\windows-simple.ps1 -Version "1.0.4"

# Deploy to network share
xcopy /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"

# Users run from
# \\server\Setups\VbdlisTools\Haihv.Vbdlis.Tools.Desktop.exe
```

**Output:** `dist/network-share/`

**Ưu điểm:**
- ✅ Đơn giản nhất
- ✅ Không cần dependencies
- ✅ Phù hợp network share
- ✅ Portable deployment

---

### Windows - Velopack Auto-Update

**Yêu cầu:**
- .NET 9.0 ASP.NET Core Runtime
- `dotnet tool install --global vpk`

```powershell
# Build
.\build\windows-velopack.ps1 -Version "1.0.4"

# Deploy
Copy-Item "dist\velopack\*" "\\server\Setups\vbdlis-tools" -Recurse
```

**Output:** `dist/velopack/`
- `VbdlisTools-1.0.4-win-Setup.exe` - Installer
- `VbdlisTools-1.0.4-win-full.nupkg` - Package
- `RELEASES` - Update manifest

**Ưu điểm:**
- ✅ Auto-update tự động
- ✅ Delta updates
- ✅ Background updates
- ❌ Cần .NET 9.0 runtime cho build tool

---

### Windows - Inno Setup

**Yêu cầu:**
- Inno Setup 6.0+ (https://jrsoftware.org/isinfo.php)

```powershell
# Build
.\build\windows-innosetup.ps1 -Version "1.0.4" -CreateSetup

# Output
dist\VbdlisTools-Setup-v1.0.4.exe
```

**Ưu điểm:**
- ✅ Traditional installer
- ✅ Install to Program Files
- ✅ Add/Remove Programs
- ❌ Cần admin rights
- ❌ Không auto-update

---

### Windows - MSIX Package

**Yêu cầu:**
- Windows SDK
- Code signing certificate

```powershell
# Build
.\build\windows-msix.ps1 -Version "1.0.4.0" -Sign -CertificatePath "cert.pfx"

# Output
dist\msix\VbdlisTools-1.0.4.0.msix
```

**Ưu điểm:**
- ✅ Microsoft Store compatible
- ✅ Modern Windows standard
- ❌ Requires certificate

---

### macOS

```bash
# Build for both architectures
./build/macos.sh Release 1.0.4 both

# Or specific arch
./build/macos.sh Release 1.0.4 arm64  # Apple Silicon
./build/macos.sh Release 1.0.4 x64    # Intel
```

**Output:** `dist/macos/`
- `VbdlisTools.app-x64/` - Intel app bundle
- `VbdlisTools.app-arm64/` - ARM app bundle
- `VbdlisTools-macOS-x64-v1.0.4.dmg` - Intel DMG
- `VbdlisTools-macOS-arm64-v1.0.4.dmg` - ARM DMG

**Note:** DMG creation requires macOS

---

## 📊 So sánh nhanh

| Method | Platform | Auto-Update | Complexity | Use Case |
|--------|----------|-------------|------------|----------|
| **Simple** | Windows | ❌ | ⭐ Easy | Network share, portable |
| **Velopack** | Windows | ✅ | ⭐⭐ Medium | Production w/ auto-update |
| **Inno Setup** | Windows | ❌ | ⭐⭐ Medium | Traditional installer |
| **MSIX** | Windows | ✅ | ⭐⭐⭐ Hard | Microsoft Store |
| **macOS** | macOS | ❌ | ⭐⭐ Medium | macOS deployment |

---

## 🎯 Khuyến nghị theo use case

### Internal Network Deployment (LAN)
→ **windows-simple.ps1**
- Deploy to network share
- Users run from share or copy to local

### Production with Auto-Update
→ **windows-velopack.ps1**
- Install once
- Auto-update from server/share

### Traditional Software Distribution
→ **windows-innosetup.ps1**
- Distribute Setup.exe
- Users install like normal software

### Microsoft Store
→ **windows-msix.ps1**
- Submit to Store
- Users install from Store

### macOS Users
→ **macos.sh**
- Create DMG
- Distribute via web or network

---

## 🔧 Parameters

### Windows Scripts

```powershell
-Version "1.0.4"           # Build version
-Configuration "Release"   # Build configuration (Release/Debug)
```

**Velopack specific:**
```powershell
-UpdateUrl "https://..."   # Update server URL (optional)
```

**Inno Setup specific:**
```powershell
-CreateSetup              # Create Setup.exe
-InnoSetupPath "..."      # Custom Inno Setup path
```

**MSIX specific:**
```powershell
-Sign                     # Sign the package
-CertificatePath "..."    # Certificate file
-CertificatePassword "..."# Certificate password
-Publisher "CN=..."       # Publisher name
```

### macOS Script

```bash
./macos.sh [Configuration] [Version] [Architecture]

# Examples
./macos.sh Release 1.0.4 both
./macos.sh Release 1.0.4 arm64
./macos.sh Debug 1.0.5 x64
```

---

## 📝 Examples

### Build all variants

```powershell
# Windows simple
.\build\windows-simple.ps1 -Version "1.0.4"

# Windows with Velopack
.\build\windows-velopack.ps1 -Version "1.0.4"

# Windows with Inno Setup
.\build\windows-innosetup.ps1 -Version "1.0.4" -CreateSetup
```

```bash
# macOS both architectures
./build/macos.sh Release 1.0.4 both
```

### Deploy to network share

```powershell
# Simple deployment
xcopy /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"

# Velopack deployment
Copy-Item "dist\velopack\*" "\\server\Setups\vbdlis-tools" -Recurse
```

---

## 🆘 Troubleshooting

### Windows

**Build failed:**
```powershell
# Check .NET SDK
dotnet --version  # Should be 10.0.x

# Clean and rebuild
Remove-Item dist -Recurse -Force -ErrorAction SilentlyContinue
.\build\windows-simple.ps1
```

**Velopack failed - needs .NET 9.0:**
```powershell
# Download and install .NET 9.0 ASP.NET Core Runtime
# https://dotnet.microsoft.com/download/dotnet/9.0

# Reinstall vpk
dotnet tool uninstall --global vpk
dotnet tool install --global vpk
```

### macOS

**Permission denied:**
```bash
chmod +x build/macos.sh
./build/macos.sh Release 1.0.4 both
```

**DMG creation failed:**
- Must run on macOS
- Can skip DMG, just use .app bundle

---

## 📚 More Info

- **[../START_HERE.md](../START_HERE.md)** - Quick start guide
- **[../BUILD_DEPLOY.md](../BUILD_DEPLOY.md)** - Detailed deployment guide
- **[../DEPLOYMENT_COMPARISON.md](../DEPLOYMENT_COMPARISON.md)** - Compare all methods
- **[../VELOPACK_AVALONIA_SETUP.md](../VELOPACK_AVALONIA_SETUP.md)** - Velopack integration

---

## 🔄 Migration from old scripts

Old scripts → New scripts:

- `build-network-share.ps1` → **windows-simple.ps1**
- `build-squirrel.ps1` → **windows-velopack.ps1**
- `build-windows.ps1` → **windows-innosetup.ps1**
- `build-msix.ps1` → **windows-msix.ps1**
- `build-macos.sh` → **macos.sh**
- ~~`build-clickonce.ps1`~~ → **REMOVED** (not compatible with .NET 10)
- ~~`build-simple.ps1`~~ → **REMOVED** (merged into windows-simple.ps1)
