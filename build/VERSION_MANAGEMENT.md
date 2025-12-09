# Version Management System

## Mục đích

Hệ thống này đảm bảo **cùng một phiên bản** khi build cho nhiều platform (Windows, macOS) trong cùng một ngày.

## Cách hoạt động

### File `version.json`

File này lưu trữ thông tin version hiện tại và được chia sẻ giữa các build script:

```json
{
  "majorMinor": "1.0",
  "currentVersion": "1.0.25120905",
  "assemblyVersion": "1.0.2512.0905",
  "lastBuildDate": "2025-12-09",
  "buildNumber": 5,
  "platforms": {
    "windows": {
      "lastBuilt": "2025-12-09T10:25:29",
      "version": "1.0.25120905"
    },
    "macos": {
      "lastBuilt": null,
      "version": null
    }
  }
}
```

### Quy tắc tự động tăng version

1. **Cùng ngày**: Build number tự động tăng (01 → 02 → 03...)
2. **Ngày mới**: Build number reset về 01

### Format Version

- **Assembly Version** (4-part): `Major.Minor.YYMM.DDBB`
  - Example: `1.0.2512.0905` = Version 1.0, Dec 2025, Day 09, Build 05
  - Dùng cho .NET assembly

- **Package Version** (3-part SemVer2): `Major.Minor.YYMMDDBB`
  - Example: `1.0.25120905` = Version 1.0, Patch 25120905
  - Dùng cho Velopack

## Workflow: Build cho nhiều platform

### Scenario 1: Build Windows trước, sau đó macOS (cùng ngày)

```powershell
# Bước 1: Build Windows
.\build\windows-velopack.ps1
# Output: Version 1.0.25120901 (build #1)
# version.json: buildNumber = 1, lastBuildDate = 2025-12-09

# Bước 2: Build macOS (cùng ngày)
./build/macos.sh
# Output: Version 1.0.25120901 (CÙNG build #1)
# version.json: buildNumber = 1 (không đổi vì đã build Windows)
```

### Scenario 2: Build lại Windows sau khi đã build macOS

```powershell
# Bước 3: Build Windows lần 2 (cùng ngày)
.\build\windows-velopack.ps1
# Output: Version 1.0.25120902 (build #2 tự động tăng)
# version.json: buildNumber = 2
```

### Scenario 3: Build ngày mới

```bash
# Ngày hôm sau
./build/macos.sh
# Output: Version 1.0.25121001 (build #1, ngày mới)
# version.json: buildNumber = 1, lastBuildDate = 2025-12-10
```

## Giải pháp: Build cùng version cho cả 2 platform

### Phương án 1: Build tuần tự cùng ngày

```powershell
# 1. Build Windows
.\build\windows-velopack.ps1
# → Version: 1.0.25120901

# 2. NGAY LẬP TỨC build macOS (cùng ngày)
./build/macos.sh
# → Version: 1.0.25120901 (CÙNG version)
```

**Lưu ý**: macOS script sẽ đọc `version.json` và sử dụng cùng build number.

### Phương án 2: Build song song (recommended)

Chạy đồng thời trên 2 máy khác nhau:

```bash
# Trên máy Windows
.\build\windows-velopack.ps1

# Đồng thời trên máy macOS
./build/macos.sh
```

Cả hai sẽ dùng **cùng build number** nếu `version.json` được sync (qua git hoặc shared folder).

### Phương án 3: Manual version lock

Nếu muốn **FORCE** cùng version:

1. Chỉnh `version.json` trước khi build:
```json
{
  "buildNumber": 1,
  "lastBuildDate": "2025-12-09"
}
```

2. Build cả Windows và macOS → Cả hai dùng build #1

3. Khi xong, tự tăng `buildNumber` lên 2 để build tiếp theo không bị trùng

## Version Control (Git)

### Option 1: Commit `version.json` (Recommended)

✅ **Ưu điểm**: Team members sync version, tránh conflict
```bash
git add build/version.json
git commit -m "chore: update build version to 1.0.25120901"
```

### Option 2: Ignore `version.json`

❌ **Nhược điểm**: Mỗi developer có version khác nhau

Thêm vào `.gitignore`:
```
build/version.json
```

Mỗi developer có file local riêng.

## Troubleshooting

### Vấn đề: Build macOS sau Windows tăng build number

**Nguyên nhân**: macOS script tự động tăng khi đọc version.json

**Giải pháp**: 
- Đảm bảo macOS script chỉ đọc (không tăng) nếu platform khác đã build
- Hoặc lock version trước khi build cả 2 platform

### Vấn đề: Version không đồng bộ giữa các máy

**Nguyên nhân**: Mỗi máy có `version.json` khác nhau

**Giải pháp**: 
- Commit `version.json` vào git
- Hoặc dùng shared folder để lưu `version.json`

## Thay đổi Major.Minor version

Edit `.csproj`:
```xml
<Version>2.0.2512.0901</Version>
```

Script sẽ tự động đọc Major.Minor (2.0) từ đó.

## Summary

| Tình huống | Windows Build # | macOS Build # | Kết quả |
|-----------|----------------|---------------|---------|
| Build Windows → Build macOS (cùng ngày) | 1 | 1 | ✅ CÙNG version |
| Build Windows → Build Windows (cùng ngày) | 1 → 2 | - | Tăng lên build 2 |
| Build ngày mới | - | 1 | Reset về build 1 |

**Best Practice**: Build cả Windows + macOS trong cùng 1 lần để đảm bảo cùng version!
