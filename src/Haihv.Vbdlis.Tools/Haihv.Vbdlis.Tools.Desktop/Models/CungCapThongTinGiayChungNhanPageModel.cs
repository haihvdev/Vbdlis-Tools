using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Haihv.Vbdlis.Tools.Desktop.Models;


public static class CungCapThongTinGiayChungNhanPayload
{
    /// <summary>
    /// Tạo payload cho tìm kiếm nâng cao thông tin giấy chứng nhận.
    /// </summary>
    /// <param name="soPhatHanh">Số phát hành của giấy chứng nhận.</param>
    /// <param name="soGiayTo">Số giấy tờ của Chủ sử dụng.</param>
    /// <param name="tinhId">Mã tỉnh, mặc định là 24 (Bắc Ninh mới).</param>
    /// <returns>Chuỗi payload đã được định dạng cho việc tìm kiếm nâng cao.</returns>
    /// <exception cref="ArgumentNullException">Nếu cả <paramref name="soPhatHanh"/> và <paramref name="soGiayTo"/> đều null.</exception>
    public static string GetAdvancedSearchGiayChungNhanPayload(string? soPhatHanh = null, string? soGiayTo = null,
        int tinhId = 24)
    {
        if (tinhId <= 0) tinhId = 24;
        if (string.IsNullOrWhiteSpace(soPhatHanh) && string.IsNullOrWhiteSpace(soGiayTo))
            throw new ArgumentNullException(nameof(soPhatHanh), "Số Phát Hành hoặc Số Giấy Tờ không được để trống");
        var formData = new StringBuilder();
        // Payload đúng theo mô tả trong issue
        formData.Append("draw=2&");
        formData.Append("columns%5B0%5D%5Bdata%5D=&");
        formData.Append("columns%5B0%5D%5Bname%5D=&");
        formData.Append("columns%5B0%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B0%5D%5Borderable%5D=false&");
        formData.Append("columns%5B0%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B0%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B1%5D%5Bdata%5D=GiayChungNhan&");
        formData.Append("columns%5B1%5D%5Bname%5D=GiayChungNhan&");
        formData.Append("columns%5B1%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B1%5D%5Borderable%5D=false&");
        formData.Append("columns%5B1%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B1%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B2%5D%5Bdata%5D=ChuSoHuu&");
        formData.Append("columns%5B2%5D%5Bname%5D=ChuSoHuu&");
        formData.Append("columns%5B2%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B2%5D%5Borderable%5D=false&");
        formData.Append("columns%5B2%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B2%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B3%5D%5Bdata%5D=TaiSan&");
        formData.Append("columns%5B3%5D%5Bname%5D=TaiSan&");
        formData.Append("columns%5B3%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B3%5D%5Borderable%5D=false&");
        formData.Append("columns%5B3%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B3%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("start=0&");
        formData.Append("length=10&");
        formData.Append("search%5Bvalue%5D=&");
        formData.Append("search%5Bregex%5D=false&");
        formData.Append("isAdvancedSearch=true&");
        formData.Append($"tinhId={tinhId}&");
        formData.Append("xaId=0&");
        formData.Append("huyenId=0&");
        formData.Append("timChinhXac=true&");
        formData.Append("andOperator=false&");
        formData.Append("loaiGiayChungNhanId=&");
        formData.Append("maVach=&");
        if (!string.IsNullOrWhiteSpace(soPhatHanh))
        {
            formData.Append($"soPhatHanh={WebUtility.UrlEncode(soPhatHanh)}&");
        }
        else
        {
            formData.Append("soPhatHanh=&");
        }

        formData.Append("soVaoSo=&");
        formData.Append("soHoSoGoc=&");
        formData.Append("soHoSoGocCu=&");
        formData.Append("soVaoSoCu=&");
        formData.Append("hoTen=&");
        formData.Append("namSinh=&");
        if (!string.IsNullOrWhiteSpace(soGiayTo))
        {
            formData.Append($"soGiayTo={WebUtility.UrlEncode(soGiayTo)}&");
        }
        else
        {
            formData.Append("soGiayTo=&");
        }

        formData.Append("soThuTuThua=&");
        formData.Append("soHieuToBanDo=&");
        formData.Append("soThuTuThuaCu=&");
        formData.Append("soHieuToBanDoCu=&");
        formData.Append("soNha=&");
        formData.Append("diaChiChiTiet=");

        return formData.ToString();
    }

    /// <summary>
    /// Tạo payload cho tìm kiếm nâng cao thông tin giấy chứng nhận.
    /// </summary>
    /// <param name="thuTuThua">Số thứ tự thửa của giấy chứng nhận.</param>
    /// <param name="toBanDo">Số tờ bản đồ của giấy chứng nhận.</param>
    /// <param name="xaId">Xã ID của giấy chứng nhận.</param>
    /// <param name="tinhId">Tỉnh ID của giấy chứng nhận. Mặc định là 24 (Tỉnh Bắc Ninh mới - Tỉnh Bắc Giang cũ).</param>
    /// <returns>Chuỗi payload đã được định dạng cho việc tìm kiếm nâng cao.</returns>
    /// <exception cref="ArgumentNullException">
    /// Nếu cả <paramref name="xaId"/>, <paramref name="toBanDo"/>, <paramref name="thuTuThua"/>, <paramref name="tinhId"/> đều nhỏ hơn hoặc bằng 0.</exception>
    public static string GetAdvancedSearchGiayChungNhanPayload(int thuTuThua, int toBanDo, int xaId, int tinhId = 24)
    {
        if (tinhId <= 0)
            tinhId = 24;
        if (toBanDo <= 0 || thuTuThua <= 0 || xaId <= 0)
            throw new ArgumentNullException(nameof(xaId), "Xã ID, Số Thửa, Số Tờ phải lớn hơn 0");
        var formData = new StringBuilder();
        // Payload đúng theo mô tả trong issue
        formData.Append("draw=2&");
        formData.Append("columns%5B0%5D%5Bdata%5D=&");
        formData.Append("columns%5B0%5D%5Bname%5D=&");
        formData.Append("columns%5B0%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B0%5D%5Borderable%5D=false&");
        formData.Append("columns%5B0%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B0%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B1%5D%5Bdata%5D=GiayChungNhan&");
        formData.Append("columns%5B1%5D%5Bname%5D=GiayChungNhan&");
        formData.Append("columns%5B1%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B1%5D%5Borderable%5D=false&");
        formData.Append("columns%5B1%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B1%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B2%5D%5Bdata%5D=ChuSoHuu&");
        formData.Append("columns%5B2%5D%5Bname%5D=ChuSoHuu&");
        formData.Append("columns%5B2%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B2%5D%5Borderable%5D=false&");
        formData.Append("columns%5B2%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B2%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B3%5D%5Bdata%5D=TaiSan&");
        formData.Append("columns%5B3%5D%5Bname%5D=TaiSan&");
        formData.Append("columns%5B3%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B3%5D%5Borderable%5D=false&");
        formData.Append("columns%5B3%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B3%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("start=0&");
        formData.Append("length=10&");
        formData.Append("search%5Bvalue%5D=&");
        formData.Append("search%5Bregex%5D=false&");
        formData.Append("isAdvancedSearch=true&");
        formData.Append($"tinhId={tinhId}&");
        formData.Append($"xaId={xaId}&");
        formData.Append("huyenId=0&");
        formData.Append("timChinhXac=true&");
        formData.Append("andOperator=false&");
        formData.Append("loaiGiayChungNhanId=&");
        formData.Append("maVach=&");
        formData.Append("soPhatHanh=&");
        formData.Append("soVaoSo=&");
        formData.Append("soHoSoGoc=&");
        formData.Append("soHoSoGocCu=&");
        formData.Append("soVaoSoCu=&");
        formData.Append("hoTen=&");
        formData.Append("namSinh=&");
        formData.Append("soGiayTo=&");
        formData.Append($"soThuTuThua={thuTuThua}&");
        formData.Append($"soHieuToBanDo={toBanDo}&");
        formData.Append("soThuTuThuaCu=&");
        formData.Append("soHieuToBanDoCu=&");
        formData.Append("soNha=&");
        formData.Append("diaChiChiTiet=");

        return formData.ToString();
    }

}

/// <summary>
/// Model phản hồi cho API tìm kiếm Giấy chứng nhận theo cấu trúc JSON đã cung cấp trong issue.
/// </summary>
public class AdvancedSearchGiayChungNhanResponse
{
    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("data")] public List<GiayChungNhanItem> Data { get; set; } = [];

    [JsonPropertyName("recordsTotal")] public int? RecordsTotal { get; set; }

    [JsonPropertyName("recordsFiltered")] public int? RecordsFiltered { get; set; }

    // Một số API có thể trả thêm statusText
    [JsonPropertyName("statusText")] public string? StatusText { get; set; }
    public bool IsError => StatusText?.Contains("error", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Deserialize JSON string sang SearchGiayChungNhanResponse
    /// </summary>
    public static AdvancedSearchGiayChungNhanResponse? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        return JsonSerializer.Deserialize<AdvancedSearchGiayChungNhanResponse>(json, options);
    }
}


public class GiayChungNhanItem
{
    [JsonPropertyName("GiayChungNhan")] public GiayChungNhanDto? GiayChungNhan { get; set; }

    [JsonPropertyName("ChuSoHuu")] public List<ChuSoHuuDto> ChuSoHuu { get; set; } = [];

    [JsonPropertyName("TaiSan")] public List<TaiSanDto> TaiSan { get; set; } = [];
}

public class GiayChungNhanDto
{
    [JsonPropertyName("ModifiedDate")] public string? ModifiedDate { get; set; }
    [JsonPropertyName("giayChungNhanId")] public long GiayChungNhanId { get; set; }

    [JsonPropertyName("loaiGiayChungNhanId")]
    public int? LoaiGiayChungNhanId { get; set; }

    [JsonPropertyName("quyetDinhId")] public long? QuyetDinhId { get; set; }
    [JsonPropertyName("canCuPhapLyId")] public long? CanCuPhapLyId { get; set; }
    [JsonPropertyName("canCuPhapLy")] public string? CanCuPhapLy { get; set; }
    [JsonPropertyName("daCongNhanPhapLy")] public bool? DaCongNhanPhapLy { get; set; }

    [JsonPropertyName("tinhTrangGiayChungNhan")]
    public int? TinhTrangGiayChungNhan { get; set; }

    [JsonPropertyName("soPhatHanh")] public string? SoPhatHanh { get; set; }
    [JsonPropertyName("soVaoSo")] public string? SoVaoSo { get; set; }
    [JsonPropertyName("ngayVaoSo")] public string? NgayVaoSo { get; set; }
    [JsonPropertyName("soVaoSoCu")] public string? SoVaoSoCu { get; set; }
    [JsonPropertyName("soHoSoGocCu")] public string? SoHoSoGocCu { get; set; }
    [JsonPropertyName("maVachCapTinh")] public bool? MaVachCapTinh { get; set; }
    [JsonPropertyName("maVach")] public string? MaVach { get; set; }
    [JsonPropertyName("coInMaVach")] public bool? CoInMaVach { get; set; }
    [JsonPropertyName("donViCap")] public int? DonViCap { get; set; }
    [JsonPropertyName("coQuanCap")] public string? CoQuanCap { get; set; }
    [JsonPropertyName("uyQuyenKy")] public bool? UyQuyenKy { get; set; }
    [JsonPropertyName("kyThay")] public bool? KyThay { get; set; }
    [JsonPropertyName("quyenGiamDoc")] public bool? QuyenGiamDoc { get; set; }
    [JsonPropertyName("phanCongCNVDK")] public bool? PhanCongCnvdk { get; set; }
    [JsonPropertyName("chinhQuyenDoThi")] public bool? ChinhQuyenDoThi { get; set; }

    [JsonPropertyName("tenNguoiKy")] public string? TenNguoiKy { get; set; }

    // API trả số (ví dụ: 11293) nên dùng kiểu số nguyên nullable để tương thích
    [JsonPropertyName("soHoSoGoc")] public long? SoHoSoGoc { get; set; }
    [JsonPropertyName("ghiChuTrang1")] public string? GhiChuTrang1 { get; set; }
    [JsonPropertyName("ghiChuTrang2")] public string? GhiChuTrang2 { get; set; }
    [JsonPropertyName("ghiChuTrang3")] public string? GhiChuTrang3 { get; set; }
    [JsonPropertyName("ghiChuTrang4")] public string? GhiChuTrang4 { get; set; }

    [JsonPropertyName("thayDoiTrongQuaTrinhSuDung")]
    public string? ThayDoiTrongQuaTrinhSuDung { get; set; }

    [JsonPropertyName("coInSoDo")] public bool? CoInSoDo { get; set; }

    [JsonPropertyName("inGiayNguoiDaiDien")]
    public bool? InGiayNguoiDaiDien { get; set; }

    [JsonPropertyName("inVoChong")] public bool? InVoChong { get; set; }
    [JsonPropertyName("inThuaKe")] public bool? InThuaKe { get; set; }
    [JsonPropertyName("soHopDong")] public string? SoHopDong { get; set; }
    [JsonPropertyName("ngayHopDong")] public string? NgayHopDong { get; set; }
    [JsonPropertyName("noiKyHopDong")] public string? NoiKyHopDong { get; set; }
    [JsonPropertyName("thongTinHopDong")] public string? ThongTinHopDong { get; set; }
    [JsonPropertyName("ghiChuHopDong")] public string? GhiChuHopDong { get; set; }
    [JsonPropertyName("soCongChung")] public string? SoCongChung { get; set; }
    [JsonPropertyName("ngayCongChung")] public string? NgayCongChung { get; set; }
    [JsonPropertyName("noiCongChung")] public string? NoiCongChung { get; set; }
    [JsonPropertyName("xaId")] public int? XaId { get; set; }
    [JsonPropertyName("huyenId")] public int? HuyenId { get; set; }
    [JsonPropertyName("tinhId")] public int? TinhId { get; set; }
    [JsonPropertyName("soThuaPhu")] public string? SoThuaPhu { get; set; }
    [JsonPropertyName("soToPhu")] public string? SoToPhu { get; set; }
    [JsonPropertyName("maGiayChungNhan")] public string? MaGiayChungNhan { get; set; }
    [JsonPropertyName("maQRCode")] public string? MaQrcode { get; set; }
    [JsonPropertyName("capGiayLanDau")] public bool? CapGiayLanDau { get; set; }
    [JsonPropertyName("version")] public int? Version { get; set; }
    [JsonPropertyName("isLastest")] public bool? IsLastest { get; set; }
    [JsonPropertyName("dangKyQuyen")] public string? DangKyQuyen { get; set; }
    [JsonPropertyName("soBienNhan")] public string? SoBienNhan { get; set; }
    [JsonPropertyName("nguoiDaiDienId")] public long? NguoiDaiDienId { get; set; }

    [JsonPropertyName("versionNguoiDaiDien")]
    public int? VersionNguoiDaiDien { get; set; }

    [JsonPropertyName("maDonViInGiay")] public string? MaDonViInGiay { get; set; }
    [JsonPropertyName("nguoiNhanId")] public long? NguoiNhanId { get; set; }
    [JsonPropertyName("versionNguoiNhan")] public int? VersionNguoiNhan { get; set; }
    [JsonPropertyName("hinhThucSuDung")] public int? HinhThucSuDung { get; set; }
    [JsonPropertyName("_id")] public long? UnderscoreId { get; set; }

    [JsonPropertyName("localId")] public string? LocalId { get; set; }

    // Thay bằng DTO chi tiết để ánh xạ rõ ràng với JSON trả về từ API
    [JsonPropertyName("ListDangKyQuyen")] public List<DangKyQuyenDto>? ListDangKyQuyen { get; set; }

    [JsonPropertyName("LoaiGiayChungNhan")]
    public LoaiGiayChungNhanDetailDto? LoaiGiayChungNhan { get; set; }

    [JsonPropertyName("ThongTinQuyetDinh")]
    public object? ThongTinQuyetDinh { get; set; }

    [JsonPropertyName("ThongTinCanCuPhapLy")]
    public object? ThongTinCanCuPhapLy { get; set; }

    [JsonPropertyName("isChange")] public bool? IsChange { get; set; }

    [JsonPropertyName("ListGhiNoNghiaVuTaiChinh")]
    public object? ListGhiNoNghiaVuTaiChinh { get; set; }

    [JsonPropertyName("ListHanCheQuyen")] public object? ListHanCheQuyen { get; set; }

    [JsonPropertyName("ListNghiaVuTaiChinh")]
    public object? ListNghiaVuTaiChinh { get; set; }

    [JsonPropertyName("ListNoiDungThayDoi")]
    public object? ListNoiDungThayDoi { get; set; }

    [JsonPropertyName("ChuSoHuu")] public object? ChuSoHuu { get; set; }
    [JsonPropertyName("TaiSan")] public object? TaiSan { get; set; }
    [JsonPropertyName("bienDongId")] public long? BienDongId { get; set; }
    [JsonPropertyName("hoSoTiepNhanId")] public string? HoSoTiepNhanId { get; set; }
    [JsonPropertyName("Id")] public string? Id { get; set; }
    [JsonPropertyName("Title")] public string? Title { get; set; }
    [JsonPropertyName("Description")] public string? Description { get; set; }
    [JsonPropertyName("Name")] public string? Name { get; set; }
    [JsonPropertyName("Path")] public string? Path { get; set; }
    [JsonPropertyName("ParentPath")] public string? ParentPath { get; set; }
    [JsonPropertyName("Layer")] public string? Layer { get; set; }
    [JsonPropertyName("InId")] public string? InId { get; set; }
    [JsonPropertyName("OutId")] public string? OutId { get; set; }
}

public class ChuSoHuuDto
{
    [JsonPropertyName("gioiTinh")] public int? GioiTinh { get; set; }
    [JsonPropertyName("hoTen")] public string? HoTen { get; set; }
    [JsonPropertyName("namSinh")] public string? NamSinh { get; set; }
    [JsonPropertyName("soGiayTo")] public string? SoGiayTo { get; set; }
    [JsonPropertyName("diaChi")] public string? DiaChi { get; set; }
}

public class TaiSanDto
{
    [JsonPropertyName("soThuTuThua")] public int? SoThuTuThua { get; set; }
    [JsonPropertyName("soHieuToBanDo")] public int? SoHieuToBanDo { get; set; }
    [JsonPropertyName("diaChi")] public string? DiaChi { get; set; }
    [JsonPropertyName("soHieuCanHo")] public string? SoHieuCanHo { get; set; }
}

// Chi tiết Loại Giấy Chứng Nhận (khớp với mẫu JSON)
public class LoaiGiayChungNhanDetailDto
{
    [JsonPropertyName("loaiGiayChungNhanId")]
    public int? LoaiGiayChungNhanId { get; set; }

    [JsonPropertyName("maLoai")] public string? MaLoai { get; set; }
    [JsonPropertyName("tenLoai")] public string? TenLoai { get; set; }
    [JsonPropertyName("laGiayDat")] public bool? LaGiayDat { get; set; }
    [JsonPropertyName("sapXep")] public int? SapXep { get; set; }

    [JsonPropertyName("trangThai")] public bool? TrangThai { get; set; }

    // Metadata chung
    [JsonPropertyName("Id")] public string? Id { get; set; }
    [JsonPropertyName("Title")] public string? Title { get; set; }
    [JsonPropertyName("Description")] public string? Description { get; set; }
    [JsonPropertyName("Name")] public string? Name { get; set; }
    [JsonPropertyName("Path")] public string? Path { get; set; }
    [JsonPropertyName("ParentPath")] public string? ParentPath { get; set; }
    [JsonPropertyName("Layer")] public string? Layer { get; set; }
    [JsonPropertyName("InId")] public string? InId { get; set; }
    [JsonPropertyName("OutId")] public string? OutId { get; set; }
}

// DTO rút gọn cho Thửa đất trong ListDangKyQuyen
public class ThuaDatLightDto
{
    [JsonPropertyName("thuaDatId")] public long ThuaDatId { get; set; }
    [JsonPropertyName("xaId")] public int? XaId { get; set; }
    [JsonPropertyName("huyenId")] public int? HuyenId { get; set; }
    [JsonPropertyName("tinhId")] public int? TinhId { get; set; }
    [JsonPropertyName("soHieuToBanDo")] public int? SoHieuToBanDo { get; set; }
    [JsonPropertyName("soThuTuThua")] public int? SoThuTuThua { get; set; }
    [JsonPropertyName("maThua")] public string? MaThua { get; set; }
    [JsonPropertyName("dienTich")] public decimal? DienTich { get; set; }
    [JsonPropertyName("diaChi")] public string? DiaChi { get; set; }
    [JsonPropertyName("version")] public int? Version { get; set; }
    [JsonPropertyName("isLastest")] public bool IsLastest { get; set; }
}

// DTO rút gọn cho Cá nhân
public class CaNhanLightDto
{
    [JsonPropertyName("caNhanId")] public long CaNhanId { get; set; }
    [JsonPropertyName("hoTen")] public string? HoTen { get; set; }
    [JsonPropertyName("namSinh")] public int? NamSinh { get; set; }
    [JsonPropertyName("xaId")] public int? XaId { get; set; }
    [JsonPropertyName("diaChi")] public string? DiaChi { get; set; }
}

// DTO rút gọn cho Hộ gia đình
public class HoGiaDinhLightDto
{
    [JsonPropertyName("hoGiaDinhId")] public long HoGiaDinhId { get; set; }
    [JsonPropertyName("chuHoId")] public long? ChuHoId { get; set; }
    [JsonPropertyName("voChongChuHoId")] public long? VoChongChuHoId { get; set; }
    [JsonPropertyName("xaId")] public int? XaId { get; set; }
    [JsonPropertyName("diaChi")] public string? DiaChi { get; set; }
    [JsonPropertyName("version")] public int? Version { get; set; }
    [JsonPropertyName("isLastest")] public bool IsLastest { get; set; }
    [JsonPropertyName("VoChong")] public CaNhanLightDto? VoChong { get; set; }
    [JsonPropertyName("ChuHo")] public CaNhanLightDto? ChuHo { get; set; }
}

// DTO cho bản ghi Đăng ký quyền trong mảng ListDangKyQuyen
public class DangKyQuyenDto
{
    [JsonPropertyName("CreatedDate")] public string? CreatedDate { get; set; }
    [JsonPropertyName("ModifiedDate")] public string? ModifiedDate { get; set; }

    [JsonPropertyName("dangKyQuyenSuDungId")]
    public long DangKyQuyenSuDungId { get; set; }

    [JsonPropertyName("tinhHinhDangKyId")] public long TinhHinhDangKyId { get; set; }
    [JsonPropertyName("loaiDoiTuong")] public int? LoaiDoiTuong { get; set; }
    [JsonPropertyName("chuSuDungId")] public long? ChuSuDungId { get; set; }
    [JsonPropertyName("caNhanId")] public long? CaNhanId { get; set; }
    [JsonPropertyName("nhomNguoiId")] public long? NhomNguoiId { get; set; }
    [JsonPropertyName("typeItem")] public int? TypeItem { get; set; }
    [JsonPropertyName("itemId")] public long? ItemId { get; set; }
    [JsonPropertyName("subItemId")] public string? SubItemId { get; set; }
    [JsonPropertyName("versionChu")] public int? VersionChu { get; set; }
    [JsonPropertyName("versionItem")] public int? VersionItem { get; set; }
    [JsonPropertyName("dangKyTaiSanId")] public long? DangKyTaiSanId { get; set; }
    [JsonPropertyName("giayChungNhanId")] public long? GiayChungNhanId { get; set; }

    [JsonPropertyName("versionGiayChungNhan")]
    public int? VersionGiayChungNhan { get; set; }

    [JsonPropertyName("hanCheQuyenId")] public long? HanCheQuyenId { get; set; }

    [JsonPropertyName("nghiaVuTaiChinhId")]
    public long? NghiaVuTaiChinhId { get; set; }

    [JsonPropertyName("HoGiaDinh")] public HoGiaDinhLightDto? HoGiaDinh { get; set; }
    [JsonPropertyName("ThuaDat")] public ThuaDatLightDto? ThuaDat { get; set; }

    // Metadata chung
    [JsonPropertyName("Id")] public string? Id { get; set; }
    [JsonPropertyName("Title")] public string? Title { get; set; }
    [JsonPropertyName("Description")] public string? Description { get; set; }
    [JsonPropertyName("Name")] public string? Name { get; set; }
    [JsonPropertyName("Path")] public string? Path { get; set; }
    [JsonPropertyName("ParentPath")] public string? ParentPath { get; set; }
    [JsonPropertyName("Layer")] public string? Layer { get; set; }
    [JsonPropertyName("InId")] public string? InId { get; set; }
    [JsonPropertyName("OutId")] public string? OutId { get; set; }
}