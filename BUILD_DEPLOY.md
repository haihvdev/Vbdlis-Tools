# Hướng dẫn Build và Deploy VBDLIS Tools

Tài liệu này mô tả toàn bộ quy trình build và phân phối ứng dụng trên Windows và macOS bằng các script mới.

## Yêu cầu

- .NET 10.0 SDK
- Windows: PowerShell 5.1+ (để chạy script `.ps1`)
- macOS: Bash + `hdiutil` (có sẵn trên macOS)

## Quan trọng ⚠️

**Playwright browsers KHÔNG được đóng gói trong mỗi bản build.** Ứng dụng sẽ tự tải khi chạy lần đầu (~300MB) và lưu ở `%LOCALAPPDATA%\ms-playwright` (Windows) hoặc `~/Library/Caches/ms-playwright` (macOS). Cách này giữ bộ cài nhỏ gọn và luôn dùng browser mới nhất.

---

## Build & Deploy cho Windows

### 1. `windows-simple.ps1` — Network share / Portable ⭐

```powershell
.\build\windows-simple.ps1 -Version "1.0.4"

# Deploy lên network share
xcopy /E /I "dist\network-share\*" "\\server\Setups\VbdlisTools\"
```

**Output:** `dist/network-share/`

**Use case:**
- Nội bộ LAN, portable hoặc chạy trực tiếp từ share
- Không yêu cầu quyền admin, không cần cài đặt thêm

**Update:** Build version mới rồi copy đè folder `dist\network-share\*` lên share.

---

### 2. `windows-velopack.ps1` — Auto-update installer

**Yêu cầu:**
- .NET 9.0 ASP.NET Core Runtime (để chạy Velopack CLI)
- `dotnet tool install --global vpk`

```powershell
.\build\windows-velopack.ps1 -Version "1.0.4"
Copy-Item "dist\velopack\*" "\\server\Setups\vbdlis-tools" -Recurse
```

**Output:** `dist/velopack/`
- `VbdlisTools-1.0.4-win-Setup.exe`
- `VbdlisTools-1.0.4-win-full.nupkg`
- `RELEASES`

**Ưu điểm:** Auto-update, delta packages, chạy không cần admin và có thể đặt update server là network share hoặc web server. Thêm hướng dẫn tích hợp code trong [`VELOPACK_AVALONIA_SETUP.md`](VELOPACK_AVALONIA_SETUP.md).

---

### 3. `windows-innosetup.ps1` — Traditional Setup.exe

**Yêu cầu:** Inno Setup 6.0+ (`ISCC.exe` trong PATH hoặc chỉ định bằng `-InnoSetupPath`).

```powershell
.\build\windows-innosetup.ps1 -Version "1.0.4" -CreateSetup
```

**Output:**
- `dist/windows/` — thư mục publish
- `dist/VbdlisTools-Setup-v1.0.4.exe`

**Use case:** Cần cài vào `Program Files`, đăng ký Add/Remove Programs, hỗ trợ silent install (`/SILENT`). Không có auto-update.

---

### 4. `windows-msix.ps1` — Microsoft Store / App Installer

**Yêu cầu:**
- Windows SDK 10.0.19041.0+
- Certificate để ký gói (PFX + password)

```powershell
.\build\windows-msix.ps1 -Version "1.0.4.0" `
    -Sign `
    -CertificatePath "cert.pfx" `
    -CertificatePassword "password"
```

**Output:** `dist/msix/VbdlisTools-1.0.4.0.msix`

**Use case:** Phát hành qua Microsoft Store hoặc phân phối nội bộ bằng App Installer. Cần import certificate trước khi cài đặt.

---

### 5. Build thủ công (nâng cao)

```powershell
cd src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop

dotnet publish `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output publish\win-x64 `
    -p:PublishReadyToRun=true

# Xóa Playwright browsers nếu có
Remove-Item -Path "publish\win-x64\.playwright" -Recurse -Force -ErrorAction SilentlyContinue
```

Có thể dùng output này để tạo ZIP hoặc làm input cho các công cụ đóng gói khác (WiX, SCCM, v.v.).

---

## Build cho macOS

### 1. `macos.sh` — Universal build (Intel + Apple Silicon)

```bash
chmod +x build/macos.sh
./build/macos.sh Release 1.0.4 both

# Hoặc chỉ build cho một kiến trúc
./build/macos.sh Release 1.0.4 x64
./build/macos.sh Release 1.0.4 arm64
```

**Output:** `dist/macos/`
- `VbdlisTools.app-x64/`
- `VbdlisTools.app-arm64/`
- `VbdlisTools-macOS-*.dmg`

DMG chỉ tạo được khi chạy trên macOS.

---

### 2. Build thủ công (nâng cao)

```bash
cd src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop

dotnet publish \
    --configuration Release \
    --runtime osx-x64 \
    --self-contained true \
    --output publish/osx-x64

dotnet publish \
    --configuration Release \
    --runtime osx-arm64 \
    --self-contained true \
    --output publish/osx-arm64

rm -rf publish/osx-*/.playwright
```

Có thể dùng `create-dmg` hoặc `hdiutil` để đóng gói thủ công nếu muốn tùy chỉnh sâu.

---

## Triển khai & Update

| Use case | Script | Ghi chú |
|----------|--------|---------|
| LAN / portable | `windows-simple.ps1` | Copy folder lên share, users chạy trực tiếp hoặc dùng `Install-ToLocal.ps1` |
| Auto-update | `windows-velopack.ps1` | Upload `dist/velopack/` lên share/web để client tự cập nhật |
| Setup truyền thống | `windows-innosetup.ps1` | Yêu cầu quyền admin, không auto-update |
| Microsoft Store / App Installer | `windows-msix.ps1` | Bắt buộc ký số |
| macOS users | `macos.sh` | Phát hành file `.dmg` hoặc `.app` |

---

## Troubleshooting nhanh

- **Build PowerShell lỗi?** Kiểm tra `dotnet --version` (>= 10.0.x) và chạy lại script với `-Configuration Release`.
- **Velopack báo thiếu runtime?** Cài `.NET 9.0 ASP.NET Core Runtime`, sau đó `dotnet tool install --global vpk`.
- **Không chạy được từ network share?** Dùng `Get-ChildItem \\share -Recurse | Unblock-File` và đảm bảo người dùng có quyền đọc.
- **macOS DMG tạo thất bại?** Bắt buộc chạy trên macOS; có thể bỏ qua DMG và phát hành trực tiếp `.app`.

Xem thêm:
- [`build/README.md`](build/README.md) – Tổng quan các script
- [`DEPLOYMENT_COMPARISON.md`](DEPLOYMENT_COMPARISON.md) – So sánh ưu/nhược điểm
- [`VELOPACK_AVALONIA_SETUP.md`](VELOPACK_AVALONIA_SETUP.md) – Tích hợp auto-update vào app
- [`CLICKONCE_MIGRATION.md`](CLICKONCE_MIGRATION.md) – Vì sao không dùng ClickOnce
