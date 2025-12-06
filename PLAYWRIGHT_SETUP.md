# Hướng dẫn cài đặt Playwright

## Yêu cầu

Trước khi chạy ứng dụng, bạn cần cài đặt Playwright browsers.

## Cài đặt

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

- Browsers sẽ được cài đặt vào thư mục: `%USERPROFILE%\AppData\Local\ms-playwright`
- Kích thước khoảng 300MB cho Chromium
- Chỉ cần cài đặt 1 lần, các lần sau không cần cài lại

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
