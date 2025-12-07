# Hướng dẫn cài đặt Playwright

## Tự động cài đặt (Khuyến nghị) ✨

**Từ phiên bản mới nhất, ứng dụng sẽ tự động cài đặt Playwright browsers khi khởi động lần đầu trên Windows và MacOS!**

- ✅ **Windows**: Tự động cài đặt
- ✅ **MacOS**: Tự động cài đặt
- ❌ **Linux**: Chưa hỗ trợ, cần cài đặt thủ công (xem bên dưới)

Khi khởi động ứng dụng lần đầu:
1. Ứng dụng sẽ kiểm tra xem Playwright browsers đã được cài đặt chưa
2. Nếu chưa có, sẽ tự động tải xuống và cài đặt Chromium browser (~300MB)
3. Quá trình này chỉ diễn ra một lần duy nhất
4. Các lần khởi động sau sẽ bỏ qua bước này

**Lưu ý**: Quá trình tải xuống và cài đặt có thể mất vài phút tùy thuộc vào tốc độ mạng. Bạn có thể theo dõi tiến trình trong log của ứng dụng.

---

## Cài đặt thủ công (Không bắt buộc cho Windows/MacOS)

Nếu bạn muốn cài đặt thủ công hoặc đang sử dụng Linux, hãy làm theo các bước sau:

### Cách 1: Sử dụng PowerShell (Khuyến nghị)

```powershell
# Di chuyển đến thư mục project
cd G:\source\haitnmt\Vbdlis-Tools\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop

# Cài đặt Playwright browsers
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

### Cách 2: Sử dụng dotnet tool

```bash
# Cài đặt playwright tool globally
dotnet tool install --global Microsoft.Playwright.CLI

# Cài đặt browsers
playwright install chromium
```

### Cách 3: Chạy trực tiếp từ NuGet package

```bash
cd G:\source\haitnmt\Vbdlis-Tools\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop
dotnet build
cd bin/Debug/net10.0
./playwright.ps1 install chromium
```

## Kiểm tra cài đặt

Sau khi cài đặt xong, chạy ứng dụng và thử đăng nhập. Trình duyệt Chromium sẽ tự động mở và thực hiện đăng nhập.

## Lưu ý

- **Vị trí cài đặt browsers**:
  - Windows: `%USERPROFILE%\AppData\Local\ms-playwright`
  - MacOS: `~/Library/Caches/ms-playwright`
  - Linux: `~/.cache/ms-playwright`
- Kích thước khoảng 300MB cho Chromium
- Chỉ cần cài đặt 1 lần, các lần sau không cần cài lại
- Việc cài đặt tự động chỉ chạy một lần khi lần đầu tiên ứng dụng phát hiện chưa có browsers

## Xử lý lỗi

### Lỗi: "Playwright executable doesn't exist"

Chạy lại lệnh cài đặt browsers ở trên.

### Lỗi: "Failed to launch browser"

1. Kiểm tra xem browsers đã được cài đặt chưa
2. Thử xóa thư mục `%USERPROFILE%\AppData\Local\ms-playwright` và cài lại

## Chế độ Headless

Mặc định browser sẽ hiển thị UI (headless=false) để bạn thấy quá trình đăng nhập.

Để chạy ẩn, sửa trong `LoginViewModel.cs`:

```csharp
await _playwrightService.InitializeAsync(headless: true);
```
