# How to Create GitHub Releases

## 🎯 Mục đích

Tài liệu này hướng dẫn cách tạo releases trên GitHub cho VBDLIS Tools với Velopack auto-update.

## 📋 Prerequisites

1. ✅ Code đã được commit và push lên GitHub
2. ✅ Build scripts đã được test local (windows-velopack.ps1 và macos.sh)
3. ✅ Version trong `build/version.json` đã được cập nhật

## 🚀 Workflow Tự Động (Recommended)

### Bước 1: Commit & Push code

```bash
git add .
git commit -m "feat: add new features for v1.0.25120905"
git push origin main
```

### Bước 2: Tạo Git Tag

```bash
# Format: v[version-number]
# Ví dụ: v1.0.25120905
VERSION="1.0.25120905"
git tag -a "v$VERSION" -m "Release version $VERSION"
git push origin "v$VERSION"
```

### Bước 3: GitHub Actions tự động chạy

Sau khi push tag, GitHub Actions sẽ:
1. ✅ Build Windows (Velopack package)
2. ✅ Build macOS arm64 (Apple Silicon)
3. ✅ Build macOS x64 (Intel)
4. ✅ Tạo GitHub Release với tất cả artifacts
5. ✅ Upload files:
   - `VbdlisTools-[version]-win-Setup.exe`
   - `VbdlisTools-[version]-win-full.nupkg`
   - `VbdlisTools-[version]-osx-arm64.zip`
   - `VbdlisTools-[version]-osx-x64.zip`
   - `RELEASES` (manifest file)

### Bước 4: Kiểm tra Release

1. Vào **GitHub Repository** → **Releases**
2. Release mới sẽ xuất hiện với tất cả files
3. Download và test trên các platform

## 🔧 Manual Release (Nếu cần)

### Option 1: Trigger từ GitHub UI

1. Vào **GitHub Repository** → **Actions**
2. Chọn workflow **Build and Release**
3. Click **Run workflow**
4. Chọn branch và click **Run workflow**

### Option 2: Build Local và Upload

```powershell
# Windows
.\build\windows-velopack.ps1

# macOS (chạy trên Mac)
./build/macos.sh Release both  # Build cả arm64 và x64
```

Sau đó:
1. Vào **GitHub** → **Releases** → **Draft a new release**
2. Chọn tag (hoặc tạo tag mới)
3. Upload các files từ:
   - `dist/velopack/*` (Windows)
   - `dist/velopack-macos/arm64/*` (macOS ARM)
   - `dist/velopack-macos/x64/*` (macOS Intel)
4. Điền Release notes
5. **Publish release**

## 📝 Release Notes Template

```markdown
## 🎉 VBDLIS Tools v1.0.25120905

### 📦 Downloads

#### Windows
- **VbdlisTools-1.0.25120905-win-Setup.exe** - Installer cho người dùng mới
- Hỗ trợ auto-update qua Velopack

#### macOS
- **VbdlisTools-1.0.25120905-osx-arm64.zip** - Cho Mac M1/M2/M3 (Apple Silicon)
- **VbdlisTools-1.0.25120905-osx-x64.zip** - Cho Mac Intel
- Hỗ trợ auto-update qua Velopack

### 🚀 Installation

**Windows:**
1. Tải file `VbdlisTools-1.0.25120905-win-Setup.exe`
2. Chạy installer
3. Ứng dụng tự động update khi có phiên bản mới

**macOS:**
1. Tải file `.zip` phù hợp với chip của bạn
2. Giải nén và kéo `VbdlisTools.app` vào thư mục Applications
3. Lần đầu chạy: Right-click → Open (để bypass Gatekeeper)
4. Ứng dụng tự động update khi có phiên bản mới

### ✨ What's New

- [Tính năng 1]
- [Tính năng 2]
- [Cải thiện 1]

### 🐛 Bug Fixes

- [Fix 1]
- [Fix 2]

### ⚠️ Breaking Changes

- [Nếu có thay đổi không tương thích ngược]

### 📚 Documentation

- Updated setup guide
- Added troubleshooting section

---

**Full Changelog**: https://github.com/haitnmt/Vbdlis-Tools/compare/v1.0.0...v1.0.25120905
```

## 🔄 Auto-Update Flow

### Khi user đã cài đặt ứng dụng:

1. **App khởi động** → Kiểm tra GitHub Releases
2. **Phát hiện version mới** → Download delta package
3. **Download hoàn tất** → Thông báo user restart
4. **User restart app** → Update được apply tự động

### Files cần thiết trên GitHub Release:

```
Release v1.0.25120905/
├── VbdlisTools-1.0.25120905-win-Setup.exe     # New install
├── VbdlisTools-1.0.25120905-win-full.nupkg    # Full package
├── VbdlisTools-1.0.25120904-1.0.25120905-win-delta.nupkg  # Delta (auto-generated)
├── RELEASES                                    # Manifest
├── VbdlisTools-1.0.25120905-osx-arm64.zip     # macOS ARM
└── VbdlisTools-1.0.25120905-osx-x64.zip       # macOS Intel
```

## 🎯 Best Practices

### 1. Version Numbering

Sử dụng format: `Major.Minor.YYMMDDBB`
- Example: `1.0.25120905` = Version 1.0, Dec 9 2025, Build 05
- Tăng Major khi có breaking changes
- Tăng Minor khi thêm features
- Build number tự động tăng

### 2. Tag Format

```bash
# Stable release
git tag -a "v1.0.25120905" -m "Release v1.0.25120905"

# Beta release
git tag -a "v1.0.25120905-beta" -m "Beta v1.0.25120905"

# Pre-release
git tag -a "v1.0.25120905-rc1" -m "Release Candidate 1"
```

### 3. Testing Before Release

```bash
# 1. Build local
.\build\windows-velopack.ps1

# 2. Test install
# Chạy VbdlisTools-[version]-win-Setup.exe

# 3. Test update
# Tạo fake old version và test update flow

# 4. Nếu OK → Push tag
git tag -a "v1.0.25120905" -m "Release v1.0.25120905"
git push origin "v1.0.25120905"
```

### 4. Release Checklist

- [ ] Code đã được test kỹ
- [ ] Version number đã được cập nhật
- [ ] Release notes đã được chuẩn bị
- [ ] Build local thành công
- [ ] Test install/update thành công
- [ ] Commit và push code
- [ ] Tạo và push git tag
- [ ] Đợi GitHub Actions build xong
- [ ] Verify release trên GitHub
- [ ] Download và test từ GitHub Release
- [ ] Thông báo cho users

## 🔍 Troubleshooting

### GitHub Actions build failed

**Check logs:**
1. Vào **Actions** tab
2. Click vào workflow run bị fail
3. Xem logs để tìm lỗi

**Common issues:**
- .NET SDK version không đúng → Update workflow
- Velopack CLI không install được → Check network
- Build script có lỗi → Test local trước

### Release không tạo được

**Permissions:**
1. Vào **Settings** → **Actions** → **General**
2. Trong **Workflow permissions**, chọn **Read and write permissions**
3. Save changes

### Auto-update không hoạt động

**Check:**
1. GitHub Release phải là **public release** (không phải draft/pre-release)
2. Files `RELEASES` phải có trong release
3. App phải config đúng GitHub repo trong `UpdateService.cs`

## 📊 Monitoring Releases

### Download Statistics

Xem trong **GitHub** → **Insights** → **Traffic** → **Popular content**

### Update Rate

Track trong app logs:
```csharp
_logger.Information("Update check: {Result}", hasUpdate ? "Available" : "Up-to-date");
_logger.Information("Update download: {Progress}%", progress);
```

## 🎉 Summary

### Quick Release Steps:

```bash
# 1. Commit changes
git add .
git commit -m "feat: new features"
git push

# 2. Create and push tag
git tag -a "v1.0.25120905" -m "Release v1.0.25120905"
git push origin "v1.0.25120905"

# 3. Wait for GitHub Actions (5-10 minutes)

# 4. Check release on GitHub
# Done! 🎉
```

Users sẽ tự động nhận update khi mở app lần tiếp theo!
