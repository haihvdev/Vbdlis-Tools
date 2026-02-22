# Hướng dẫn cho Claude Code

## Quy tắc tạo Commit Message

Tất cả commit message phải được viết bằng **tiếng Việt** có dấu.

### Định dạng: `<type>: <mô tả ngắn gọn>`

**Type** (giữ nguyên tiếng Anh):
- `feat`: Tính năng mới
- `fix`: Sửa lỗi
- `refactor`: Tái cấu trúc code
- `docs`: Cập nhật tài liệu
- `style`: Định dạng code (không ảnh hưởng logic)
- `test`: Thêm/sửa test
- `chore`: Cập nhật build, dependencies

**Mô tả ngắn gọn** (dòng đầu tiên):
- Viết bằng tiếng Việt có dấu
- Ngắn gọn, rõ ràng (tối đa 72 ký tự)
- Bắt đầu bằng động từ: "bổ sung", "cập nhật", "sửa", "xóa", "thêm", "cải thiện", "tối ưu"
- Liệt kê nhiều thay đổi chính cách nhau bằng dấu chấm phẩy (;)

**Chi tiết** (body - bắt buộc khi commit phức tạp):
- Thêm một dòng trống sau mô tả ngắn
- Giải thích **tại sao** thay đổi này cần thiết
- Liệt kê các thay đổi chính dưới dạng bullet points
- Đề cập đến các file/component quan trọng bị ảnh hưởng
- Ghi chú breaking changes nếu có
- Viết bằng tiếng Việt có dấu

### Ví dụ:

```
feat: bổ sung chức năng đăng nhập VBDLIS
fix: sửa lỗi hiển thị dữ liệu ĐVHC
refactor: tái cấu trúc DatabaseService và DvhcCacheService
chore: cập nhật dependencies và cấu hình build
```

```
feat: bổ sung chức năng đăng nhập VBDLIS tự động

Thêm service xử lý đăng nhập và quản lý phiên làm việc với VBDLIS:
- Tạo VbdlisAuthService để xử lý login/logout
- Lưu trữ trạng thái đăng nhập trong SessionManager
- Tự động kiểm tra phiên trước khi thực hiện upload

Các file chính: VbdlisAuthService.cs, SessionManager.cs
```

### Lưu ý:
- Với commit đơn giản: chỉ cần mô tả ngắn gọn
- Với commit phức tạp (nhiều thay đổi, refactor lớn): **BẮT BUỘC** thêm phần chi tiết
- Breaking changes luôn cần ghi chú rõ ràng trong body
