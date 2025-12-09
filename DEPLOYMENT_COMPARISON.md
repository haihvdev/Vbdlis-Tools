# So sánh các phương pháp Deployment cho VBDLIS Tools

## Tóm tắt nhanh

| Phương pháp | Auto-Update | Cần Admin | Tương thích .NET 10 | Độ phức tạp | Khuyến nghị |
|-------------|-------------|-----------|---------------------|-------------|-------------|
| **Velopack (Squirrel)** | ✅ Có | ❌ Không | ✅ Có | ⭐⭐ Dễ | **🏆 Tốt nhất** |
| **MSIX** | ✅ Có* | ❌ Không | ✅ Có | ⭐⭐⭐ Trung bình | Tốt cho Store |
| **Inno Setup** | ❌ Không | ✅ Có | ✅ Có | ⭐⭐ Dễ | Backup option |
| **ZIP Archive** | ❌ Không | ❌ Không | ✅ Có | ⭐ Rất dễ | Dev/Test only |
| ~~ClickOnce~~ | ❌ N/A | ❌ N/A | ❌ **KHÔNG** | N/A | **❌ Không dùng** |

*MSIX auto-update qua Microsoft Store hoặc App Installer

---

## 1. Velopack (Squirrel successor) 🚀

### Ưu điểm
- ✅ **Auto-update tích hợp**: Delta updates, tiết kiệm bandwidth
- ✅ **Không cần admin**: Cài vào `%LOCALAPPDATA%`
- ✅ **Background updates**: Không làm gián đoạn người dùng
- ✅ **Tương thích hoàn toàn**: .NET 10.0 + Avalonia
- ✅ **Open source**: Active maintenance, community support
- ✅ **Dễ deploy**: Chỉ cần upload folder lên web server

### Nhược điểm
- ❌ Không cài vào Program Files
- ❌ Cần web server cho auto-update (có thể dùng GitHub Releases)
- ❌ Cần thêm code trong app để check updates

### Khi nào dùng
- ✅ Triển khai cho nhiều users
- ✅ Cần auto-update
- ✅ Không có certificate signing
- ✅ Deploy qua network share hoặc web server

### Script
```powershell
.\build\windows-velopack.ps1 -Version "1.0.5"
```

### Kích thước
- Setup.exe: ~100-150MB (không bao gồm Playwright)
- Delta updates: Chỉ tải phần thay đổi (~5-20MB/update)

---

## 2. MSIX Package 📦

### Ưu điểm
- ✅ **Chuẩn Microsoft mới nhất**: Future-proof
- ✅ **Sandbox security**: Cài đặt an toàn, isolated
- ✅ **Microsoft Store**: Có thể publish lên Store
- ✅ **App Installer**: Auto-update qua .appinstaller file
- ✅ **Clean uninstall**: Không để lại rác

### Nhược điểm
- ❌ **BẮT BUỘC certificate**: Phải ký với trusted cert
- ❌ **User phải trust cert**: Thao tác thêm cho end-user
- ❌ **Phức tạp hơn**: Cần Windows SDK, MakeAppx.exe
- ❌ **Hạn chế APIs**: Một số Windows APIs bị block trong sandbox

### Khi nào dùng
- ✅ Có certificate signing infrastructure
- ✅ Muốn publish lên Microsoft Store
- ✅ Doanh nghiệp có PKI
- ✅ Cần security cao

### Script
```powershell
.\build\windows-msix.ps1 -Version "1.0.5.0" -Sign -CertificatePath "cert.pfx"
```

### Kích thước
- .msix file: ~100-150MB (không bao gồm Playwright)

---

## 3. Inno Setup (Truyền thống)

### Ưu điểm
- ✅ **Cài vào Program Files**: Như phần mềm truyền thống
- ✅ **Tùy biến cao**: Custom wizard, screens, actions
- ✅ **Đăng ký Windows**: Add/Remove Programs đầy đủ
- ✅ **Silent install**: `/SILENT` flag
- ✅ **Không cần certificate**: Hoạt động mà không cần ký

### Nhược điểm
- ❌ **Cần admin rights**: Users phải có quyền admin
- ❌ **Không auto-update**: Phải download installer mới thủ công
- ❌ **Cồng kềnh**: User phải chạy uninstaller cũ → installer mới

### Khi nào dùng
- ✅ Users có admin rights
- ✅ Không cần auto-update (update ít)
- ✅ Muốn cài vào Program Files
- ✅ Dùng GitHub Releases để distribute

### Script
```powershell
.\build\windows-innosetup.ps1 -Version "1.0.5" -CreateSetup
```

### Kích thước
- Setup.exe: ~100-150MB (không bao gồm Playwright)

---

## 4. ZIP Archive (Dev/Test)

### Ưu điểm
- ✅ **Đơn giản nhất**: Chỉ cần giải nén
- ✅ **Portable**: Chạy ở bất kỳ đâu
- ✅ **Không cần install**: Xcopy deployment

### Nhược điểm
- ❌ Không có shortcuts
- ❌ Không tự update
- ❌ Không đăng ký với Windows
- ❌ Users phải tự quản lý

### Khi nào dùng
- ✅ Development/Testing
- ✅ Quick distribution cho tech-savvy users
- ✅ Portable deployments

### Script
```powershell
.\build\windows-simple.ps1 -Version "1.0.5"
```

---

## ❌ ClickOnce - KHÔNG tương thích

### Tại sao không dùng?
- ❌ **Chỉ hỗ trợ .NET Framework 4.x** (WPF/WinForms cũ)
- ❌ **KHÔNG hỗ trợ .NET 5+** (bao gồm .NET 10.0)
- ❌ **KHÔNG hỗ trợ Avalonia**
- ❌ Manifest format không tương thích

### Lỗi khi dùng
```
Exception from HRESULT: 0x80070C81
Parsing and DOM creation of the manifest resulted in error
```

### Thay thế
Dùng **Velopack** - tính năng tương tự ClickOnce nhưng tương thích .NET modern

---

## Khuyến nghị theo use case

### 🏢 Doanh nghiệp nội bộ (LAN)
**→ Velopack + Network Share**
- Deploy `dist/velopack/` lên network share
- Users chạy Setup.exe từ share
- Auto-update từ share

### 🌐 Internet deployment (Public)
**→ Velopack + Web Server**
- Upload `dist/velopack/` lên web server
- Users download Setup.exe
- Auto-update từ web URL

### 🏪 Microsoft Store
**→ MSIX**
- Build MSIX với certificate
- Submit to Microsoft Store
- Users cài từ Store, auto-update

### 💼 Enterprise với PKI
**→ MSIX + Group Policy**
- Sign với enterprise certificate
- Deploy qua SCCM/Intune
- Managed updates

### 👨‍💻 Tech users / GitHub
**→ Inno Setup + GitHub Releases**
- Upload Setup.exe lên GitHub Releases
- Users download từ Releases page
- Manual update

### 🧪 Testing / Development
**→ ZIP Archive**
- Extract and run
- No installation needed

---

## Migration Path

### Từ ZIP → Velopack
1. Build với Velopack: `.\build\windows-velopack.ps1`
2. Users chạy Setup.exe (uninstall bản portable nếu muốn)
3. Từ nay app tự update

### Từ Inno Setup → Velopack
1. Users uninstall version cũ (qua Add/Remove Programs)
2. Chạy Velopack Setup.exe
3. Từ nay auto-update, không cần admin

### Từ ZIP/Inno → MSIX
1. Tạo self-signed certificate hoặc mua certificate
2. Build MSIX signed
3. Users install certificate + MSIX
4. Auto-update qua App Installer

---

## Code ví dụ: Auto-update với Velopack

### 1. Thêm NuGet package

```bash
dotnet add package Velopack
```

### 2. Implement update check

```csharp
using Velopack;

public class UpdateService
{
    private const string UpdateUrl = "https://your-server.com/vbdlis-tools/";

    public async Task<bool> CheckAndApplyUpdates()
    {
        try
        {
            var mgr = new UpdateManager(UpdateUrl);

            var release = await mgr.CheckForUpdatesAsync();
            if (release != null)
            {
                await mgr.DownloadUpdatesAsync(release);
                mgr.ApplyUpdatesAndRestart(release);
                return true; // sẽ restart sau khi apply
            }

            return false; // No updates
        }
        catch (Exception ex)
        {
            // Log error, continue without update
            Console.WriteLine($"Update check failed: {ex.Message}");
            return false;
        }
    }
}
```

### 3. Call on startup

```csharp
public partial class App : Application
{
    public override async void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        // Check for updates (non-blocking)
        _ = Task.Run(async () =>
        {
            var updateService = new UpdateService();
            var needsRestart = await updateService.CheckAndApplyUpdates();

            if (needsRestart)
            {
                // Show notification to user
                Dispatcher.UIThread.Post(() =>
                {
                    // Show "Restart to apply updates" message
                });
            }
        });
    }
}
```

---

## Tổng kết

### 🥇 Lựa chọn #1: Velopack
- Tốt nhất cho hầu hết trường hợp
- Auto-update miễn phí
- Không cần admin
- Dễ setup

### 🥈 Lựa chọn #2: MSIX
- Tốt nếu có certificate
- Tốt cho Microsoft Store
- Modern, future-proof

### 🥉 Lựa chọn #3: Inno Setup
- Backup option
- Phù hợp nếu không cần auto-update
- Traditional deployment

### ❌ Tránh: ClickOnce
- Không tương thích .NET 10.0
- Lỗi manifest
- Dùng Velopack thay thế
