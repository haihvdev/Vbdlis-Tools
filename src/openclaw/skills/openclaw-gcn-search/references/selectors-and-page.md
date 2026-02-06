# VBDLIS GCN page selectors (CungCapThongTinGiayChungNhan)

Target page:
- https://bgi.mplis.gov.vn/dc/CungCapThongTinGiayChungNhan/Index

Useful readiness selectors:
- `#cung_cap_thong_tin_wrapper`
- `h2.app-title` (often: "Cung cấp thông tin giấy chứng nhận")
- `input[name="soGiayTo"]`
- `input[name="soPhatHanh"]`
- `#btnTraCuuGiayChungNhan`
- `#tblTraCuuGiayChungNhan`
- `#tblTraCuuGiayChungNhan_info`

Empty-table selector:
- `td.dataTables_empty`

Login redirect pattern:
- URL contains `/account/login` on `https://authen.mplis.gov.vn/...`
- Inputs: `input[placeholder="Tên tài khoản"]`, `input[placeholder="Mật khẩu"]`
- Button: role=button name="ĐĂNG NHẬP"
