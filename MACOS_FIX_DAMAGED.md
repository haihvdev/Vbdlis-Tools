# Fix "App is damaged" Error trên macOS

## Vấn đề
Khi mở VbdlisTools trên macOS, bạn gặp lỗi:
```
"VbdlisTools.app is damaged and can't be opened. You should move it to the Trash."
```

## Nguyên nhân
- App không được ký số (code sign) bởi Apple Developer Certificate
- macOS Gatekeeper chặn các app không được xác thực
- Quarantine attribute được gán cho file tải từ internet

## Giải pháp nhanh (Khuyến nghị)

### Cách 1: Xóa Quarantine Attribute
```bash
xattr -cr /Applications/VbdlisTools.app
```

**Các bước chi tiết:**
1. Mở **Terminal** (Applications → Utilities → Terminal)
2. Copy và paste lệnh trên
3. Nhấn Enter
4. Mở VbdlisTools bình thường

### Cách 2: Bypass Gatekeeper (Tạm thời)
```bash
# Tắt Gatekeeper (không khuyến nghị cho security)
sudo spctl --master-disable

# Sau khi mở app, bật lại
sudo spctl --master-enable
```

### Cách 3: Allow app cụ thể
```bash
sudo xattr -rd com.apple.quarantine /Applications/VbdlisTools.app
```

### Cách 4: Qua System Settings
1. Mở **System Settings** → **Privacy & Security**
2. Cuộn xuống phần **Security**
3. Nếu thấy thông báo về VbdlisTools → Click **Open Anyway**

## Giải pháp lâu dài (Cho Developer)

### Option 1: Self-Signed Certificate (Free)
```bash
# Tạo self-signed certificate
security find-identity -v -p codesigning

# Sign app
codesign --deep --force --verify --verbose --sign "Developer ID" VbdlisTools.app

# Verify
codesign --verify --deep --strict --verbose=2 VbdlisTools.app
```

### Option 2: Apple Developer Program ($99/năm)
1. Đăng ký [Apple Developer Program](https://developer.apple.com/programs/)
2. Tạo Developer ID certificate
3. Sign app với certificate
4. Notarize app với Apple (bắt buộc cho macOS 10.15+)

**Thêm vào macos.sh:**
```bash
# Code signing
if [ -n "$APPLE_DEVELOPER_ID" ]; then
    echo "Signing app with Developer ID..."
    codesign --deep --force --verify --verbose \
        --sign "$APPLE_DEVELOPER_ID" \
        --options runtime \
        "$APP_BUNDLE"
    
    # Notarize
    echo "Notarizing app..."
    xcrun notarytool submit "$DMG_PATH" \
        --apple-id "$APPLE_ID" \
        --password "$APP_SPECIFIC_PASSWORD" \
        --team-id "$TEAM_ID" \
        --wait
    
    # Staple
    xcrun stapler staple "$DMG_PATH"
fi
```

### Option 3: Ad-hoc Signing (GitHub Actions)
```bash
# Ad-hoc signing (không verify với Apple nhưng tốt hơn unsigned)
codesign --force --deep --sign - VbdlisTools.app
```

## GitHub Actions Setup (với Apple Developer)

Thêm secrets vào GitHub:
```yaml
secrets:
  APPLE_DEVELOPER_ID: ${{ secrets.APPLE_DEVELOPER_ID }}
  APPLE_ID: ${{ secrets.APPLE_ID }}
  APP_SPECIFIC_PASSWORD: ${{ secrets.APP_SPECIFIC_PASSWORD }}
  TEAM_ID: ${{ secrets.TEAM_ID }}
```

## Tài liệu tham khảo
- [Apple Code Signing Guide](https://developer.apple.com/library/archive/documentation/Security/Conceptual/CodeSigningGuide/)
- [Notarizing macOS Software](https://developer.apple.com/documentation/security/notarizing_macos_software_before_distribution)
- [Gatekeeper Documentation](https://support.apple.com/guide/security/gatekeeper-sec5599b66df)

## Notes
- **Cách 1** là đơn giản nhất cho người dùng
- **Apple Developer Program** cần nếu muốn distribute rộng rãi
- **Self-signed** chỉ work trên máy của bạn
- **Ad-hoc signing** tốt hơn unsigned nhưng vẫn cần xattr fix
