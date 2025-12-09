# Hướng dẫn Build và Deploy VBDLIS Tools

Tài liệu này hướng dẫn cách build và tạo file cài đặt cho Windows và macOS.

## Yêu cầu

- .NET 10.0 SDK hoặc mới hơn
- **Windows**: PowerShell 5.1+ (để chạy build scripts)
- **macOS**: Bash, hdiutil (tích hợp sẵn trong macOS)

## Quan trọng ⚠️

**Playwright browsers KHÔNG được bao gồm trong bản build/installer**. Ứng dụng sẽ tự động tải và cài đặt Playwright browsers khi chạy lần đầu tiên trên Windows hoặc macOS.

Lý do:
- Giảm kích thước file cài đặt (~300MB)
- Luôn sử dụng phiên bản Playwright mới nhất
- Tránh lỗi tương thích giữa các hệ điều hành

---

## Build cho Windows

### Cách 1: Build đơn giản (chỉ publish files)

```powershell
# Build Windows x64
.\build\build-simple.ps1 -Platform windows

# Hoặc build tất cả platforms
.\build\build-simple.ps1 -Platform all
```

Output: `dist/windows-x64/`

### Cách 2: Build và tạo ZIP package

```powershell
# Chạy script build Windows
.\build\build-windows.ps1

# Hoặc chỉ định version
.\build\build-windows.ps1 -Version "1.2.0"
```

Output:
- `dist/windows/` - Folder chứa files
- `dist/VbdlisTools-Windows-x64-v1.0.0.zip` - ZIP package

### Cách 3: Build thủ công

```powershell
cd src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop

# Publish
dotnet publish `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output publish\win-x64 `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true

# Remove Playwright browsers nếu có
Remove-Item -Path "publish\win-x64\.playwright" -Recurse -Force -ErrorAction SilentlyContinue
```

### Cách 4: Velopack - Auto-Update Installer (Khuyến nghị cho .NET modern) 🚀

**LƯU Ý QUAN TRỌNG**: ClickOnce truyền thống **KHÔNG tương thích** với .NET 10.0 và Avalonia. Sử dụng **Velopack** thay thế - một giải pháp tương tự ClickOnce cho .NET modern.

**Velopack** (kế thừa Squirrel.Windows) cung cấp tính năng tương tự ClickOnce: cài đặt dễ dàng, tự động cập nhật, delta updates.

**Yêu cầu**:
- .NET 10.0 SDK
- Velopack CLI tool (vpk)

```powershell
# Cài đặt Velopack tool (chỉ cần 1 lần)
dotnet tool install --global vpk

# Build với Velopack
.\build\build-squirrel.ps1

# Build với version cụ thể
.\build\build-squirrel.ps1 -Version "1.0.5"

# Build với update URL
.\build\build-squirrel.ps1 -Version "1.0.5" -UpdateUrl "https://your-server.com/vbdlis-tools/"
```

**Output:**
- `dist/velopack/VbdlisTools-1.0.5-win-Setup.exe` - Installer cho người dùng mới
- `dist/velopack/VbdlisTools-1.0.5-win-full.nupkg` - Full package
- `dist/velopack/RELEASES` - Manifest file cho auto-update

**Tính năng:**
- ✅ **Tự động cập nhật** với delta updates (chỉ tải phần thay đổi)
- ✅ Không cần quyền Administrator
- ✅ Cài đặt vào `%LOCALAPPDATA%` (an toàn)
- ✅ Hỗ trợ rollback về phiên bản cũ
- ✅ Background updates (không làm gián đoạn người dùng)
- ✅ Tương thích với .NET 10.0 và Avalonia

**Cách triển khai:**

1. **Build installer:**
   ```powershell
   .\build\build-squirrel.ps1 -Version "1.0.5"
   ```

2. **Phân phối cho người dùng mới:**
   - Chia sẻ file `dist/velopack/VbdlisTools-1.0.5-win-Setup.exe`
   - Người dùng chạy Setup.exe để cài đặt

3. **Setup auto-update (tùy chọn):**
   - Upload tất cả files trong `dist/velopack/` lên web server hoặc network share
   - URL ví dụ: `https://your-server.com/vbdlis-tools/`
   - Network share: `\\server\share\vbdlis-tools\`

4. **Thêm code auto-update vào ứng dụng:**
   ```bash
   # Thêm NuGet package
   dotnet add package Velopack
   ```

   ```csharp
   // Thêm vào code
   using Velopack;

   public async Task CheckForUpdates()
   {
       try
       {
           var updateUrl = "https://your-server.com/vbdlis-tools/";
           // Hoặc network share: var updateUrl = @"\\server\share\vbdlis-tools";

           var mgr = new UpdateManager(updateUrl);
           var newVersion = await mgr.CheckForUpdatesAsync();

           if (newVersion != null)
           {
               // Download updates
               await mgr.DownloadUpdatesAsync(newVersion);

               // Apply and restart
               mgr.ApplyUpdatesAndRestart(newVersion);
           }
       }
       catch (Exception ex)
       {
           // Log error, continue without update
       }
   }
   ```

**Cách phát hành bản cập nhật:**

1. Build version mới:
   ```powershell
   .\build\build-squirrel.ps1 -Version "1.0.6"
   ```

2. Copy tất cả files mới lên cùng vị trí
   - Upload lên web server hoặc network share
   - Velopack tự động tạo delta packages
   - Người dùng chỉ tải phần thay đổi

3. Ứng dụng tự động phát hiện và cập nhật

**Ưu điểm so với ClickOnce:**
- ✅ **Tương thích .NET 10.0** và Avalonia
- ✅ Delta updates (tiết kiệm bandwidth)
- ✅ Background updates (UX tốt hơn)
- ✅ **Hỗ trợ network share** (không bắt buộc web server)
- ✅ Open source, active development

**So với Inno Setup:**
- ✅ Auto-update tích hợp sẵn
- ✅ Không cần quyền admin
- ✅ Delta updates tiết kiệm băng thông
- ❌ Không cài vào Program Files

---

### Cách 5: MSIX Package (Chuẩn mới của Microsoft) 📦

**MSIX** là định dạng package hiện đại của Microsoft, thay thế ClickOnce và MSI.

**Yêu cầu**:
- .NET 10.0 SDK
- Windows SDK 10.0.19041.0+
- Certificate để ký (bắt buộc)

```powershell
# Build MSIX package
.\build\build-msix.ps1

# Build với version và ký số
.\build\build-msix.ps1 -Version "1.0.5.0" -Sign -CertificatePath "cert.pfx" -CertificatePassword "pass"
```

**Output:**
- `dist/msix/VbdlisTools-1.0.5.0.msix` - MSIX package

**Tính năng:**
- ✅ Chuẩn mới nhất của Windows
- ✅ Cài đặt an toàn (sandbox)
- ✅ Tích hợp Microsoft Store
- ✅ Auto-update qua Store hoặc App Installer
- ✅ Dễ uninstall, không để lại rác

**Cài đặt:**

```powershell
# Cài đặt MSIX
Add-AppxPackage -Path "VbdlisTools-1.0.5.0.msix"

# Hoặc double-click file .msix
```

**Lưu ý:**
- ⚠️ **BẮT BUỘC** phải ký với certificate tin cậy
- ⚠️ Người dùng cần trust certificate trước
- ✅ Phù hợp cho triển khai qua Microsoft Store
- ✅ Phù hợp cho doanh nghiệp có PKI infrastructure

---

### ⚠️ ClickOnce Không Tương Thích

**ClickOnce truyền thống (build-clickonce.ps1) KHÔNG hoạt động** với:
- .NET 5, 6, 7, 8, 9, 10+
- Avalonia UI
- Cross-platform apps

**Lý do**: ClickOnce chỉ hỗ trợ .NET Framework 4.x (WPF/WinForms cũ)

**Giải pháp**:
- ✅ Dùng **Velopack** (dễ nhất, khuyến nghị, hỗ trợ network share)
- ✅ Dùng **MSIX** (chuẩn mới, cần certificate)
- ✅ Dùng **Inno Setup** (truyền thống, không auto-update)

---

### Cách 5: Tạo Setup.exe với Inno Setup (Alternative)

**Yêu cầu**: Inno Setup 6.0+ (tải từ https://jrsoftware.org/isinfo.php)

```powershell
# Build và tạo setup.exe
.\build\build-windows.ps1 -Version "1.0.0" -CreateSetup

# Hoặc tùy chỉnh đường dẫn Inno Setup
.\build\build-windows.ps1 -Version "1.0.0" -CreateSetup -InnoSetupPath "C:\Path\To\ISCC.exe"
```

**Output:**
- `dist/windows/` - Files
- `dist/VbdlisTools-Windows-x64-v1.0.0.zip` - ZIP
- `dist/VbdlisTools-Setup-v1.0.0.exe` - **Setup installer**

**Tính năng Setup.exe:**
- ✅ Cài đặt vào `C:\Program Files\VBDLIS Tools\`
- ✅ Tạo shortcut trên Desktop và Start Menu
- ✅ Tự động uninstall phiên bản cũ khi cập nhật
- ✅ Hỗ trợ silent install: `setup.exe /SILENT`
- ✅ Đăng ký vào Add/Remove Programs

### Tạo Windows Installer thủ công (Nâng cao)

**Option 1: Inno Setup** (Đã tích hợp trong build script)
- File script: `build/installer.iss`
- Compile: `ISCC.exe build\installer.iss`

**Option 2: WiX Toolset**
- Tạo Windows MSI installer
- https://wixtoolset.org/

**Option 3: MSIX**
- Package cho Microsoft Store
- Yêu cầu certificate để sign

---

## Build cho macOS

### Cách 1: Build đơn giản (chỉ publish files)

```bash
# Build cho cả x64 và ARM64
./build/build-simple.ps1 -Platform all

# Hoặc chỉ một architecture
./build/build-simple.ps1 -Platform macos-x64
./build/build-simple.ps1 -Platform macos-arm64
```

Output: `dist/macos-x64/` và `dist/macos-arm64/`

### Cách 2: Build và tạo .app + DMG

```bash
# Cần chạy trên macOS để tạo DMG

# Build cho cả hai architectures
chmod +x build/build-macos.sh
./build/build-macos.sh Release 1.0.0 both

# Hoặc chỉ một architecture
./build/build-macos.sh Release 1.0.0 x64
./build/build-macos.sh Release 1.0.0 arm64
```

Output:
- `dist/macos/VbdlisTools.app-x64/` - Application bundle cho Intel
- `dist/macos/VbdlisTools.app-arm64/` - Application bundle cho Apple Silicon
- `dist/macos/VbdlisTools-macOS-x64-v1.0.0.dmg` - DMG installer cho Intel
- `dist/macos/VbdlisTools-macOS-arm64-v1.0.0.dmg` - DMG installer cho Apple Silicon

### Cách 3: Build thủ công

```bash
cd src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop

# Publish for Intel Macs
dotnet publish \
    --configuration Release \
    --runtime osx-x64 \
    --self-contained true \
    --output publish/osx-x64

# Publish for Apple Silicon Macs
dotnet publish \
    --configuration Release \
    --runtime osx-arm64 \
    --self-contained true \
    --output publish/osx-arm64

# Remove Playwright browsers nếu có
rm -rf publish/osx-*/.playwright
```

---

## Kiểm tra kích thước

Sau khi build, kiểm tra kích thước:

```powershell
# Windows
Get-ChildItem -Path "dist\windows-x64" -Recurse | Measure-Object -Property Length -Sum

# macOS/Linux
du -sh dist/macos-x64
```

Kích thước dự kiến:
- **Không có Playwright**: ~100-150MB
- **Có Playwright**: ~400-450MB (KHÔNG nên bao gồm)

---

## Triển khai

### Windows

1. **Cách 1**: Giải nén ZIP và chạy `Haihv.Vbdlis.Tools.Desktop.exe`
2. **Cách 2**: Tạo installer bằng Inno Setup và phân phối file `.exe`
3. **Cách 3**: Xcopy deployment - Copy folder vào Program Files

### macOS

1. **Cách 1**: Mount DMG file và kéo .app vào Applications
2. **Cách 2**: Giải nén .app bundle và copy vào /Applications
3. **Lưu ý**: Lần đầu chạy có thể cần:
   ```bash
   xattr -cr /Applications/VbdlisTools.app
   ```
   (Để bypass Gatekeeper nếu app chưa được sign)

---

## Code Signing (Optional nhưng khuyến nghị)

### Windows
```powershell
# Sign với certificate
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com Haihv.Vbdlis.Tools.Desktop.exe
```

### macOS
```bash
# Sign app bundle
codesign --force --deep --sign "Developer ID Application: Your Name" VbdlisTools.app

# Notarize (để bypass Gatekeeper)
xcrun notarytool submit VbdlisTools.dmg --wait --apple-id your@email.com --team-id TEAMID
```

---

## Cấu trúc thư mục sau khi build

```
dist/
├── windows-x64/              # Windows build output
│   ├── Haihv.Vbdlis.Tools.Desktop.exe
│   ├── *.dll
│   └── ...
├── macos-x64/                # macOS Intel build output
│   ├── Haihv.Vbdlis.Tools.Desktop
│   ├── *.dll
│   └── ...
├── macos-arm64/              # macOS Apple Silicon build output
│   └── ...
├── macos/                    # macOS app bundles and DMGs
│   ├── VbdlisTools.app-x64/
│   ├── VbdlisTools.app-arm64/
│   ├── VbdlisTools-macOS-x64-v1.0.0.dmg
│   └── VbdlisTools-macOS-arm64-v1.0.0.dmg
└── VbdlisTools-Windows-x64-v1.0.0.zip
```

---

## Troubleshooting

### Build thất bại với lỗi "SDK not found"
```bash
# Kiểm tra .NET SDK đã cài đặt chưa
dotnet --list-sdks

# Nếu chưa có, tải từ: https://dotnet.microsoft.com/download
```

### macOS: Permission denied khi chạy script
```bash
chmod +x build/build-macos.sh
```

### Windows: Execution policy error
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Playwright không tự động cài đặt
- Kiểm tra kết nối mạng
- Xem log file trong thư mục ứng dụng
- Thử cài thủ công theo hướng dẫn trong `PLAYWRIGHT_SETUP.md`

---

## Template Inno Setup (Windows Installer)

Tạo file `installer.iss`:

```ini
[Setup]
AppName=VBDLIS Tools
AppVersion=1.0.0
DefaultDirName={autopf}\VBDLIS Tools
DefaultGroupName=VBDLIS Tools
OutputDir=dist
OutputBaseFilename=VbdlisTools-Setup-v1.0.0
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest

[Files]
Source: "dist\windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\VBDLIS Tools"; Filename: "{app}\Haihv.Vbdlis.Tools.Desktop.exe"
Name: "{autodesktop}\VBDLIS Tools"; Filename: "{app}\Haihv.Vbdlis.Tools.Desktop.exe"

[Run]
Filename: "{app}\Haihv.Vbdlis.Tools.Desktop.exe"; Description: "Launch VBDLIS Tools"; Flags: nowait postinstall skipifsilent
```

Compile:
```powershell
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

---

## Ghi chú

- Playwright browsers (~300MB) sẽ được tải về `%LOCALAPPDATA%\ms-playwright` (Windows) hoặc `~/Library/Caches/ms-playwright` (macOS)
- Chỉ cần tải một lần, các lần chạy sau sẽ dùng lại
- Nếu muốn pre-install Playwright, xem `PLAYWRIGHT_SETUP.md`
