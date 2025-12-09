# 🚀 Quick Start: Tạo Release trên GitHub

## TL;DR - Cách nhanh nhất

```powershell
# 1. Test build local
.\build-all.ps1

# 2. Tạo release
.\create-release.ps1

# 3. Đợi GitHub Actions build (~10 phút)
# Done! ✅
```

## Chi tiết từng bước

### 1️⃣ Chuẩn bị code

```powershell
# Commit changes
git add .
git commit -m "feat: add new features"
git push origin main
```

### 2️⃣ Build và test local

```powershell
# Build Windows
.\build-all.ps1

# Test installer
.\dist\velopack\VbdlisTools-*.exe
```

### 3️⃣ Tạo release tag

**Tự động (Recommended):**
```powershell
.\create-release.ps1
```

**Thủ công:**
```bash
git tag -a "v1.0.25120905" -m "Release v1.0.25120905"
git push origin "v1.0.25120905"
```

### 4️⃣ GitHub Actions tự động

Sau khi push tag, GitHub sẽ:
- ✅ Build Windows (Velopack installer)
- ✅ Build macOS arm64 (Apple Silicon)
- ✅ Build macOS x64 (Intel)
- ✅ Tạo GitHub Release
- ✅ Upload tất cả installers

**Xem tiến trình:** https://github.com/haitnmt/Vbdlis-Tools/actions

### 5️⃣ Verify release

**Kiểm tra:** https://github.com/haitnmt/Vbdlis-Tools/releases

Files nên có:
- ✅ `VbdlisTools-[version]-win-Setup.exe`
- ✅ `VbdlisTools-[version]-win-full.nupkg`
- ✅ `VbdlisTools-[version]-osx-arm64.zip`
- ✅ `VbdlisTools-[version]-osx-x64.zip`
- ✅ `RELEASES` (manifest)

### 6️⃣ Test auto-update

1. Install old version
2. Open app → Nhận thông báo update
3. Click update → Download và restart
4. App được cập nhật tự động ✅

## 📚 Đọc thêm

- **[GITHUB_RELEASES.md](GITHUB_RELEASES.md)** - Hướng dẫn chi tiết
- **[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)** - Checklist đầy đủ
- **[build/VERSION_MANAGEMENT.md](build/VERSION_MANAGEMENT.md)** - Quản lý version

## 🆘 Troubleshooting

### Build failed trên GitHub Actions?

**Kiểm tra:**
1. Vào **Actions** → Click vào run bị fail
2. Xem logs để tìm lỗi
3. Fix và push lại

### Auto-update không hoạt động?

**Check:**
1. Release phải **public** (không phải draft)
2. File `RELEASES` phải có trong release
3. `UpdateService.cs` config đúng repo

### Permission denied?

1. **Settings** → **Actions** → **General**
2. Workflow permissions: **Read and write permissions**
3. Save

## ✨ Tips

### Build cùng version cho cả Windows + macOS

```powershell
# Build Windows trước
.\build\windows-velopack.ps1
# Version: 1.0.25120901

# Build macOS ngay sau (cùng ngày)
./build/macos.sh
# Version: 1.0.25120901 (CÙNG version!)
```

### Skip build local, chỉ dùng GitHub Actions

```bash
# Chỉ cần push tag
git tag -a "v1.0.25120905" -m "Release"
git push origin "v1.0.25120905"

# GitHub tự build tất cả
```

### Pre-release / Beta version

```bash
git tag -a "v1.0.25120905-beta" -m "Beta release"
git push origin "v1.0.25120905-beta"
```

Sửa workflow để mark là pre-release:
```yaml
- name: Create Release
  uses: softprops/action-gh-release@v2
  with:
    prerelease: true  # Mark as pre-release
```

## 🎯 Best Practices

✅ **DO:**
- Test build local trước khi tạo release
- Viết release notes chi tiết
- Tag theo semantic versioning
- Commit `version.json` để team sync version

❌ **DON'T:**
- Tạo release từ untested code
- Skip version bump
- Force push tags
- Build production trên dirty working directory

## 📊 Version Format

```
v1.0.25120905
│ │  │  │  └─ Build number (01-99)
│ │  │  └──── Day (09)
│ │  └─────── Month + Year (2512 = Dec 2025)
│ └────────── Minor version
└──────────── Major version
```

Auto-increment mỗi lần build trong cùng ngày.

---

**🎉 Happy Releasing!**

Có thắc mắc? Xem [GITHUB_RELEASES.md](GITHUB_RELEASES.md) hoặc [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)
