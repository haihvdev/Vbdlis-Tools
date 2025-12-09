# 🚀 VBDLIS Tools - Hướng dẫn Deployment

## ❌ Vấn đề ClickOnce

ClickOnce **KHÔNG tương thích** với .NET 10.0 + Avalonia.

Lỗi: `HRESULT: 0x80070C81 - Exception reading manifest`

---

## ✅ Giải pháp: Network Share Deployment

### Tại sao chọn giải pháp này?

- ✅ **Đơn giản nhất** - chỉ 3 bước
- ✅ **Không cần dependencies** - chỉ .NET 10.0 SDK
- ✅ **Hoạt động ngay** với network share
- ✅ **Dễ update** - chỉ recopy files

---

## 📋 3 Bước Deploy

### Bước 1: Build

```powershell
# Build cho Windows
.\build\windows-simple.ps1 -Version "1.0.4"
```

**Output:** `dist/network-share/` chứa tất cả files

### Bước 2: Deploy lên Network Share

```powershell
# Copy lên network share
xcopy /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"
```

### Bước 3: Hướng dẫn Users

Users có 3 cách sử dụng:

**Cách 1: Chạy từ Network Share**
```
\\server\Setups\VbdlisTools\Haihv.Vbdlis.Tools.Desktop.exe
```

**Cách 2: Copy về máy**
```powershell
xcopy /E /I "\\server\Setups\VbdlisTools" "C:\Apps\VbdlisTools\"
```

**Cách 3: Cài đặt tự động**
```
Right-click: \\server\Setups\VbdlisTools\Install-ToLocal.ps1
> Run with PowerShell
```

---

## 🔄 Cách Update

```powershell
# Build version mới
.\build\windows-simple.ps1 -Version "1.0.5"

# Copy lên share (ghi đè)
xcopy /Y /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"
```

---

## 🍎 macOS Deployment

```bash
# Build cho macOS
chmod +x build/macos.sh
./build/macos.sh Release 1.0.4 both

# Output
dist/macos/VbdlisTools-macOS-*.dmg
```

---

## 📊 Build Scripts Overview

| Script | Platform | Use Case |
|--------|----------|----------|
| **windows-simple.ps1** | Windows | Network share / Portable ⭐ |
| **windows-velopack.ps1** | Windows | Auto-update installer |
| **windows-innosetup.ps1** | Windows | Traditional Setup.exe |
| **windows-msix.ps1** | Windows | Microsoft Store |
| **macos.sh** | macOS | macOS .app + DMG |

Xem chi tiết: [build/README.md](build/README.md)

---

## 🎯 Nếu cần Auto-Update (Tùy chọn)

### Option 1: Velopack (Khuyến nghị)

**Yêu cầu:**
1. Cài .NET 9.0 ASP.NET Core Runtime
2. `dotnet tool install --global vpk`

```powershell
# Build
.\build\windows-velopack.ps1 -Version "1.0.4"

# Deploy
Copy-Item "dist\velopack\*" "\\server\Setups\vbdlis-tools" -Recurse
```

Xem: [VELOPACK_AVALONIA_SETUP.md](VELOPACK_AVALONIA_SETUP.md)

### Option 2: Giữ Simple Deployment

- Không cần cài thêm gì
- Update bằng cách recopy files
- Đơn giản, dễ quản lý

---

## ❓ Troubleshooting

### Build failed?
```powershell
dotnet --version  # Check .NET 10.0
```

### Không chạy từ network share?
```powershell
# Unblock files
Get-ChildItem "\\server\Setups\VbdlisTools" -Recurse | Unblock-File
```

### Playwright không tải?
- Tự động tải lần đầu (~300MB)
- Lưu vào: `%LOCALAPPDATA%\ms-playwright`

---

## 📚 Tài liệu đầy đủ

- **[build/README.md](build/README.md)** - Hướng dẫn các script build
- **[BUILD_DEPLOY.md](BUILD_DEPLOY.md)** - Chi tiết deployment
- **[DEPLOYMENT_COMPARISON.md](DEPLOYMENT_COMPARISON.md)** - So sánh phương pháp
- **[VELOPACK_AVALONIA_SETUP.md](VELOPACK_AVALONIA_SETUP.md)** - Setup auto-update
- **[CLICKONCE_MIGRATION.md](CLICKONCE_MIGRATION.md)** - Tại sao không dùng ClickOnce

---

## 📝 Tóm tắt

```powershell
# Windows - Simple (Khuyến nghị)
.\build\windows-simple.ps1
xcopy /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"

# macOS
./build/macos.sh Release 1.0.4 both
```

✅ **XONG!** Đơn giản, nhanh, không phức tạp.
