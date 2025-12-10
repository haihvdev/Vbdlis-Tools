# VBDLIS Tools

Công cụ hỗ trợ làm việc với hệ thống VBDLIS.

## 🚀 Bắt đầu nhanh

### Build local (Windows)

```powershell
# Build local với tự động tăng version
.\build-local.ps1

# Output: dist/velopack/VbdlisTools-{version}-Setup.zip
```

### Build local (macOS)

```bash
# Build local với tự động tăng version
./build-local-macos.sh

# Output: dist/velopack-macos-local/VbdlisTools-{version}-osx-arm64.dmg
```

---

## 📦 Tạo GitHub Release

```powershell
# Bước 1: Build local (tự động tăng version)
.\build-local.ps1

# Bước 2: Tạo release (sử dụng version từ build-local.ps1)
.\create-release.ps1

# GitHub Actions sẽ:
# - Build Windows ONLY (không tăng version)
# - Tạo GitHub Release
# - Upload Windows installer
```

**Lưu ý:** macOS builds phải build local và upload thủ công lên GitHub Release.

---

## 🔧 Build Scripts

| Script | Platform | Mục đích |
|--------|----------|---------|
| **build-local.ps1** | Windows | Build local với tự động tăng version |
| **build-local-macos.sh** | macOS | Build local với tự động tăng version |
| **build\windows-velopack.ps1** | Windows | Script build (được gọi bởi build-local.ps1 và GitHub Actions) |

---

## 📝 Quản lý Version

Format version: `Major.Minor.YYMMDDBB`
- Ví dụ: `1.0.25121001`
  - `1.0` - Major.Minor version
  - `251210` - Ngày (2025-12-10)
  - `01` - Build number (tăng theo ngày)

### File Version: `build/version.json`

```json
{
  "majorMinor": "1.0",
  "currentVersion": "1.0.25121001",
  "assemblyVersion": "1.0.2512.1001",
  "lastBuildDate": "2025-12-10",
  "buildNumber": 1,
  "platforms": {
    "windows": {
      "lastBuilt": "2025-12-10T07:45:00",
      "version": "1.0.25121001"
    },
    "macos": {
      "lastBuilt": "",
      "version": ""
    }
  }
}
```

### Cơ chế tự động tăng Version

- **Local builds** (`build-local.ps1` hoặc `build-local-macos.sh`):
  - ✅ Tự động tăng version
  - ✅ Cập nhật `build/version.json`
  - ✅ Cập nhật file `.csproj`

- **GitHub Actions** (`.github/workflows/release.yml`):
  - 🔒 Sử dụng version ĐÃ KHÓA từ `build/version.json`
  - ❌ KHÔNG tự động tăng version
  - ✅ Build Windows ONLY

---

## 🛠️ Tech Stack

- **.NET 10.0** - Framework
- **Avalonia UI** - Cross-platform UI
- **SQLite** - Database
- **Playwright** - Browser automation
- **Serilog** - Logging
- **EPPlus** - Excel processing
- **Velopack** - Auto-update installer

---

## 📋 Yêu cầu hệ thống

### Để Build:
- **.NET 10.0 SDK**
- **Velopack CLI** (tự động cài bởi build scripts)

### Để chạy ứng dụng:
- **Windows 10+** hoặc **macOS 10.15+**
- **.NET 10.0 Runtime** (đã bao gồm trong installer)
- **Kết nối Internet** (lần chạy đầu tiên - ứng dụng sẽ tự động tải Chromium ~150MB)

---

## 🌐 Playwright Browsers

Ứng dụng sử dụng Playwright để tự động hóa browser. **Chromium browser KHÔNG được bundle** trong installer/DMG để giữ kích thước file nhỏ (~50MB thay vì ~200MB).

### Hành vi lần chạy đầu tiên

Khi chạy lần đầu, ứng dụng sẽ tự động:
1. Phát hiện Chromium chưa được cài đặt
2. Tải Chromium (~150MB)
3. Cài đặt vào thư mục cache của user
4. Khởi động bình thường

**Yêu cầu:**
- Kết nối Internet khi chạy lần đầu
- ~150MB dung lượng trống
- Cho phép download trong firewall/antivirus

**Lợi ích:**
- ✅ Installer/DMG nhẹ hơn (~50MB)
- ✅ Download và cài đặt nhanh hơn
- ✅ Chromium luôn được cập nhật từ Playwright
- ⚠️ Cần internet lần chạy đầu tiên

---

## 📝 License

© 2025 vpdkbacninh.vn | haihv.vn

---

## 🆘 Hỗ trợ

Nếu gặp vấn đề hoặc có câu hỏi, vui lòng mở issue trên GitHub.
