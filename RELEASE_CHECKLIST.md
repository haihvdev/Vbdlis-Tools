# Release Checklist

Sử dụng checklist này trước khi tạo release mới.

## 📋 Pre-Release Checklist

### Code Quality
- [ ] All tests pass locally
- [ ] No compiler warnings
- [ ] Code reviewed (nếu có team)
- [ ] Dependencies updated (nếu cần)

### Version Management
- [ ] Version number đã được quyết định (format: `Major.Minor.YYMMDDBB`)
- [ ] `build/version.json` đã được cập nhật (hoặc sẽ tự động cập nhật)
- [ ] Breaking changes đã được document (nếu có)

### Documentation
- [ ] README.md đã update (nếu cần)
- [ ] Changelog/Release notes đã chuẩn bị
- [ ] API changes đã được document (nếu có)

### Testing
- [ ] ✅ Build Windows thành công
  ```powershell
  .\build\windows-velopack.ps1
  ```
- [ ] ✅ Test installer Windows
  - Chạy `VbdlisTools-[version]-win-Setup.exe`
  - Kiểm tra ứng dụng hoạt động đúng
  - Test auto-update (nếu có old version)
  
- [ ] ✅ Build macOS thành công (nếu có Mac)
  ```bash
  ./build/macos.sh Release both
  ```
- [ ] ✅ Test macOS app bundle
  - Giải nén và chạy .app
  - Kiểm tra hoạt động trên cả Intel và Apple Silicon (nếu có)

### Configuration
- [ ] UpdateService.cs config đúng GitHub repo
- [ ] Icon files có đầy đủ (`.ico`, `.icns`)
- [ ] App manifest đã đúng thông tin
- [ ] Certificate/signing đã setup (nếu cần)

---

## 🚀 Release Process

### Step 1: Final Preparations

```powershell
# Update dependencies
dotnet restore

# Clean and rebuild
dotnet clean
.\build-all.ps1

# Test build outputs
cd dist/velopack
dir
```

### Step 2: Commit Changes

```bash
# Review changes
git status
git diff

# Commit
git add .
git commit -m "chore: prepare for release v1.0.25120905"

# Push
git push origin main
```

### Step 3: Create Release Tag

**Option A: Using script (Recommended)**
```powershell
.\create-release.ps1
```

**Option B: Manual**
```bash
VERSION="1.0.25120905"
git tag -a "v$VERSION" -m "Release v$VERSION

✨ What's New
- Feature 1
- Feature 2

🐛 Bug Fixes
- Fix 1
- Fix 2
"

git push origin "v$VERSION"
```

### Step 4: Monitor GitHub Actions

- [ ] Vào https://github.com/haitnmt/Vbdlis-Tools/actions
- [ ] Kiểm tra workflow **Build and Release** đang chạy
- [ ] Đợi tất cả jobs hoàn thành (~10-15 phút)
- [ ] Check logs nếu có lỗi

### Step 5: Verify Release

- [ ] Vào https://github.com/haitnmt/Vbdlis-Tools/releases
- [ ] Release mới xuất hiện
- [ ] Tất cả files đã được upload:
  - [ ] `VbdlisTools-[version]-win-Setup.exe`
  - [ ] `VbdlisTools-[version]-win-full.nupkg`
  - [ ] `RELEASES` (Windows)
  - [ ] `VbdlisTools-[version]-osx-arm64.zip`
  - [ ] `VbdlisTools-[version]-osx-x64.zip`
  - [ ] `RELEASES` (macOS, nếu có)

### Step 6: Test Release

- [ ] Download Windows installer từ GitHub
- [ ] Test install on clean machine
- [ ] Download macOS package từ GitHub
- [ ] Test install on Mac (nếu có)

### Step 7: Test Auto-Update

- [ ] Install old version
- [ ] Open app → Should detect new version
- [ ] Download and apply update
- [ ] Verify updated version

---

## 📝 Post-Release Checklist

### Communication
- [ ] Thông báo release trong team/organization
- [ ] Update internal documentation
- [ ] Notify users (email, slack, etc.)
- [ ] Post on social media (nếu cần)

### Monitoring
- [ ] Monitor GitHub Issues cho bug reports
- [ ] Check download statistics
- [ ] Monitor crash reports (nếu có)
- [ ] Track update adoption rate

### Cleanup
- [ ] Archive old release files (nếu cần)
- [ ] Update project board/issues
- [ ] Plan next release

---

## 🐛 Troubleshooting

### Build Failed on GitHub Actions

**Error: .NET SDK not found**
```yaml
# Update .github/workflows/release.yml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'  # Check version
```

**Error: Velopack CLI not found**
```bash
# Add to workflow
- name: Install Velopack CLI
  run: dotnet tool install --global vpk
```

### Release Creation Failed

**Error: Permission denied**
- Settings → Actions → General
- Workflow permissions: "Read and write permissions"
- Save changes

### Files Not Uploaded

**Check paths in workflow:**
```yaml
- name: Upload artifacts
  uses: actions/upload-artifact@v4
  with:
    path: |
      dist/velopack/*.exe
      dist/velopack/*.nupkg
```

### Auto-Update Not Working

**Check UpdateService.cs:**
```csharp
// Make sure repo is correct
private const string GitHubRepoOwner = "haitnmt";
private const string GitHubRepoName = "Vbdlis-Tools";

// GithubSource should point to releases
var source = new GithubSource(
    $"https://github.com/{GitHubRepoOwner}/{GitHubRepoName}", 
    null,  // No token for public repo
    false  // Not prerelease
);
```

---

## 📊 Release Template

Copy this for release notes:

```markdown
## 🎉 VBDLIS Tools v1.0.25120905

### ✨ What's New
- [Feature 1 description]
- [Feature 2 description]

### 🐛 Bug Fixes
- [Bug fix 1]
- [Bug fix 2]

### 🚀 Improvements
- [Improvement 1]
- [Improvement 2]

### ⚠️ Breaking Changes
- [Breaking change 1, if any]

### 📦 Downloads
- Windows: `VbdlisTools-1.0.25120905-win-Setup.exe`
- macOS (Apple Silicon): `VbdlisTools-1.0.25120905-osx-arm64.zip`
- macOS (Intel): `VbdlisTools-1.0.25120905-osx-x64.zip`

### 📚 Documentation
- [Link to docs if any]

**Full Changelog**: https://github.com/haitnmt/Vbdlis-Tools/compare/v1.0.0...v1.0.25120905
```

---

## ✅ Quick Checklist

Copy và paste vào GitHub Issue/PR:

```markdown
## Release Checklist

- [ ] Code tested locally
- [ ] Build successful (Windows + macOS)
- [ ] Version bumped
- [ ] Release notes prepared
- [ ] Changes committed
- [ ] Tag created and pushed
- [ ] GitHub Actions completed
- [ ] Release verified
- [ ] Installers tested
- [ ] Auto-update tested
- [ ] Users notified
```
