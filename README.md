# VBDLIS Tools

Công cụ hỗ trợ làm việc với hệ thống VBDLIS.

## 🚀 Quick Start

### Windows Deployment

```powershell
# Build
.\build\windows-simple.ps1 -Version "1.0.4"

# Deploy to network share
xcopy /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"
```

### macOS Deployment

```bash
# Build
./build/macos.sh Release 1.0.4 both

# Output: dist/macos/*.dmg
```

**→ Xem chi tiết:** [BUILD_DEPLOY.md](BUILD_DEPLOY.md)

---

## 📋 Build Scripts

| Script | Platform | Purpose |
|--------|----------|---------|
| **windows-simple.ps1** | Windows | Network share / Portable ⭐ |
| **windows-velopack.ps1** | Windows | Auto-update installer |
| **windows-innosetup.ps1** | Windows | Traditional Setup.exe |
| **windows-msix.ps1** | Windows | Microsoft Store package |
| **macos.sh** | macOS | .app bundle + DMG |

**→ Xem chi tiết:** [build/README.md](build/README.md)

---

## 📚 Documentation

- **[build/README.md](build/README.md)** - Hướng dẫn build scripts
- **[BUILD_DEPLOY.md](BUILD_DEPLOY.md)** - Chi tiết deployment
- **[DEPLOYMENT_COMPARISON.md](DEPLOYMENT_COMPARISON.md)** - So sánh phương pháp
- **[VELOPACK_AVALONIA_SETUP.md](VELOPACK_AVALONIA_SETUP.md)** - Setup auto-update
- **[CLICKONCE_MIGRATION.md](CLICKONCE_MIGRATION.md)** - Vì sao không dùng ClickOnce

---

## 🛠️ Tech Stack

- **.NET 10.0** - Framework
- **Avalonia UI** - Cross-platform UI framework
- **SQLite** - Database
- **Playwright** - Browser automation
- **Serilog** - Logging
- **EPPlus** - Excel processing

---

## 📦 Deployment Options

### 1. Simple Network Share (Khuyến nghị) ⭐

```powershell
.\build\windows-simple.ps1
```

- ✅ Đơn giản nhất
- ✅ Không cần dependencies
- ✅ Phù hợp LAN deployment

### 2. Auto-Update với Velopack

```powershell
.\build\windows-velopack.ps1
```

- ✅ Auto-update tự động
- ✅ Delta updates
- ⚠️ Cần .NET 9.0 runtime

### 3. Traditional Installer

```powershell
.\build\windows-innosetup.ps1 -CreateSetup
```

- ✅ Setup.exe truyền thống
- ❌ Cần admin rights

### 4. Microsoft Store

```powershell
.\build\windows-msix.ps1 -Sign
```

- ✅ Store-ready package
- ⚠️ Cần certificate

---

## ⚠️ ClickOnce Note

**ClickOnce KHÔNG tương thích** với .NET 10.0 + Avalonia.

Đã thay thế bằng các phương pháp modern. Xem [CLICKONCE_MIGRATION.md](CLICKONCE_MIGRATION.md) để biết thêm chi tiết.

---

## 🔧 Requirements

### For Building:
- **.NET 10.0 SDK** (required)
- **Windows SDK** (for MSIX)
- **Inno Setup** (for traditional installer)
- **.NET 9.0 Runtime** (for Velopack)

### For Running:
- **Windows 10+** or **macOS 10.15+**
- **.NET 10.0 Runtime** (if not self-contained)
- **Internet connection** (for Playwright first-run)

---

## 📝 License

© 2025 vpdkbacninh.vn | haihv.vn

---

## 🆘 Support

Gặp vấn đề? Check:
1. [BUILD_DEPLOY.md](BUILD_DEPLOY.md) - Chi tiết deployment
2. [build/README.md](build/README.md) - Build scripts guide
3. [VELOPACK_AVALONIA_SETUP.md](VELOPACK_AVALONIA_SETUP.md) - Auto-update
