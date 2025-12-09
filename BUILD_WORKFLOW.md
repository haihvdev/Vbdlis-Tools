# Local Build và Release Workflow

## 🎯 Mục đích

Tách riêng quá trình:
- **Local build**: Test và tăng version tự động
- **Release**: Sử dụng version từ local build để tạo GitHub release

## 📋 Workflow Mới

### Bước 1: Build Local (Test & Auto-increment)

```powershell
# Build locally với auto-increment version
.\build-local.ps1

# Output:
# Assembly Version: 1.0.2512.0901 (4-part for .NET)
# Package Version:  1.0.25120901 (3-part SemVer2 for Velopack)
# Build Number:     1
```

**Script này sẽ:**
- ✅ Đọc version từ `build\version.json`
- ✅ Tự động tăng build number (01, 02, 03...)
- ✅ Cập nhật `version.json` với version mới
- ✅ Cập nhật `.csproj` với version mới
- ✅ Build Windows installer
- ✅ Tạo files trong `dist\velopack\`

**Test installer:**
```powershell
# Run installer để test
.\dist\velopack\VbdlisTools-1.0.25120901-win-Setup.exe
```

### Bước 2: Create Release (Sử dụng version từ local)

```powershell
# Tạo release với version từ local build
.\create-release.ps1

# Script sẽ hỏi:
# 📦 Version from local build: 1.0.25120901
# Use this version for release? (y/n/custom)
```

**Script này sẽ:**
- ✅ Đọc version từ `version.json` (do `build-local.ps1` tạo)
- ✅ Tạo git tag `v1.0.25120901`
- ✅ Push tag lên GitHub
- ✅ Trigger GitHub Actions workflow
- ✅ GitHub build Windows + macOS với **CÙNG version**

## 🔄 So sánh với workflow cũ

### Workflow Cũ

```powershell
# Build và release cùng lúc
.\build-all.ps1
.\create-release.ps1

# Vấn đề:
# - Không test trước khi release
# - Version có thể khác nhau giữa local và GitHub
```

### Workflow Mới ✅

```powershell
# Bước 1: Build local, test, tăng version
.\build-local.ps1

# Bước 2: Test installer
.\dist\velopack\VbdlisTools-*.exe

# Bước 3: Nếu OK, tạo release với version đã test
.\create-release.ps1
```

## 📊 Version Management

### Auto-increment trong build-local.ps1

```powershell
# Mỗi lần chạy, version tự động tăng:
.\build-local.ps1  # Build 1: 1.0.25120901
.\build-local.ps1  # Build 2: 1.0.25120902
.\build-local.ps1  # Build 3: 1.0.25120903
```

### Sử dụng version trong create-release.ps1

```powershell
# Option 1: Dùng version từ local build (Recommended)
.\create-release.ps1
# Use this version for release? (y/n/custom): y

# Option 2: Custom version
.\create-release.ps1
# Use this version for release? (y/n/custom): custom
# Enter custom version: 1.0.25120905

# Option 3: Truyền version trực tiếp
.\create-release.ps1 -Version "1.0.25120905" -Message "Release v1.0.25120905"
```

## 🔧 Chi tiết Scripts

### build-local.ps1

**Chức năng:**
- Build Windows installer locally
- Auto-increment version theo ngày
- Update `version.json` và `.csproj`
- Tạo files trong `dist\velopack\`

**Tham số:**
```powershell
.\build-local.ps1 -Configuration Release  # Default
.\build-local.ps1 -Configuration Debug
```

**Version format:**
- Assembly: `1.0.2512.0901` (Major.Minor.YYMM.DDBB)
- Package: `1.0.25120901` (Major.Minor.YYMMDDBB)

### create-release.ps1

**Chức năng:**
- Đọc version từ `version.json`
- Tạo git tag
- Push lên GitHub
- Trigger GitHub Actions

**Tham số:**
```powershell
.\create-release.ps1                                           # Interactive
.\create-release.ps1 -Version "1.0.25120901"                   # Specific version
.\create-release.ps1 -Version "1.0.25120901" -Message "v1.0"   # With message
```

**Workflow:**
1. Đọc version từ `version.json`
2. Hỏi confirm hoặc custom version
3. Check uncommitted changes
4. Push commits
5. Create & push tag
6. Trigger GitHub Actions

## 🧪 Testing

### Test workflow hoàn chỉnh

```powershell
# 1. Build local
.\build-local.ps1

# 2. Verify version
Get-Content .\build\version.json | ConvertFrom-Json

# Expected output:
# currentVersion    : 1.0.25120901
# assemblyVersion   : 1.0.2512.0901
# lastBuildDate     : 2025-12-09
# buildNumber       : 1

# 3. Test installer
.\dist\velopack\VbdlisTools-1.0.25120901-win-Setup.exe

# 4. If OK, create release
.\create-release.ps1

# 5. Verify tag created
git tag -l "v1.0.25120901"

# 6. Check GitHub Actions
# https://github.com/haitnmt/Vbdlis-Tools/actions
```

## 💡 Use Cases

### Case 1: Development & Testing

```powershell
# Build nhiều lần để test
.\build-local.ps1  # v1.0.25120901
.\build-local.ps1  # v1.0.25120902
.\build-local.ps1  # v1.0.25120903

# Chỉ release version cuối cùng
.\create-release.ps1 -Version "1.0.25120903"
```

### Case 2: Hotfix

```powershell
# Build hotfix
.\build-local.ps1

# Test thoroughly
.\dist\velopack\VbdlisTools-*.exe

# Release nếu OK
.\create-release.ps1
```

### Case 3: Major/Minor version change

```powershell
# 1. Update version.json
$version = Get-Content .\build\version.json | ConvertFrom-Json
$version.majorMinor = "2.0"  # Change from 1.0 to 2.0
$version | ConvertTo-Json -Depth 10 | Set-Content .\build\version.json

# 2. Build with new major version
.\build-local.ps1
# Output: 2.0.25120901 (new major version)

# 3. Release
.\create-release.ps1
```

## 🎓 Best Practices

1. **Luôn build local trước** khi release
   ```powershell
   .\build-local.ps1
   ```

2. **Test installer** trước khi push tag
   ```powershell
   .\dist\velopack\*.exe
   ```

3. **Verify version** trong version.json
   ```powershell
   Get-Content .\build\version.json | ConvertFrom-Json
   ```

4. **Commit version changes** trước khi create release
   ```powershell
   git add build\version.json
   git commit -m "chore: bump version to 1.0.25120901"
   ```

5. **Document release** trong git tag message
   ```powershell
   .\create-release.ps1 -Message "feat: add new features"
   ```

## 🆘 Troubleshooting

### Q: Version không tăng?

**A:** Check `version.json`:
```powershell
Get-Content .\build\version.json | ConvertFrom-Json
```

Verify:
- `lastBuildDate` có đúng ngày hôm nay?
- `buildNumber` có giá trị hợp lệ?

### Q: Create release lỗi "version.json not found"?

**A:** Chạy `build-local.ps1` trước:
```powershell
.\build-local.ps1
.\create-release.ps1
```

### Q: Muốn release version khác với local build?

**A:** Dùng `-Version` parameter:
```powershell
.\create-release.ps1 -Version "1.0.25120905"
```

### Q: GitHub Actions build version khác?

**A:** GitHub Actions sẽ build lại với cùng version từ tag. Nếu khác, check:
1. Tag name đúng format `v1.0.25120901`
2. Workflow file đọc version từ tag
3. `version.json` đã được commit

## 📚 Related Documentation

- **[QUICKSTART_RELEASE.md](QUICKSTART_RELEASE.md)** - Quick start guide
- **[GITHUB_RELEASES.md](GITHUB_RELEASES.md)** - GitHub releases guide
- **[VERSION_LOCKING.md](VERSION_LOCKING.md)** - Version locking system
- **[build/VERSION_MANAGEMENT.md](build/VERSION_MANAGEMENT.md)** - Version details

## ✅ Summary

**Workflow mới:**
```
┌─────────────────┐
│ build-local.ps1 │  ← Build local, auto-increment version
└────────┬────────┘
         │
         v
┌─────────────────┐
│  Test locally   │  ← Test installer
└────────┬────────┘
         │
         v
┌──────────────────┐
│create-release.ps1│ ← Use version from local, create GitHub release
└────────┬─────────┘
         │
         v
┌──────────────────┐
│ GitHub Actions   │ ← Build Windows + macOS with same version
└──────────────────┘
```

**Benefits:**
- ✅ Test trước khi release
- ✅ Version nhất quán giữa local và GitHub
- ✅ Dễ rollback nếu có lỗi
- ✅ Clear separation of concerns

🎉 **Enjoy your new workflow!**
