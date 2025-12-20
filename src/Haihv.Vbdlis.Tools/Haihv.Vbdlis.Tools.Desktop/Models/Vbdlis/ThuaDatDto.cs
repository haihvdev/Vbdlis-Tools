using System.Collections.Generic;
using System.Text.Json.Serialization;
using Haihv.Vbdlis.Tools.Desktop.Models.Vbdlis.Converters;

namespace Haihv.Vbdlis.Tools.Desktop.Models.Vbdlis;

// DTO đầy đủ cho Thửa đất (từ liên kết)
public class ThuaDatFullDto
{
    [JsonPropertyName("xaId")] public int XaId { get; set; }
    [JsonPropertyName("huyenId")] public int HuyenId { get; set; }
    [JsonPropertyName("tinhId")] public int TinhId { get; set; }
    [JsonPropertyName("thuaDatId")] public long ThuaDatId { get; set; }
    [JsonPropertyName("soHieuToBanDo")] public int? SoHieuToBanDo { get; set; }
    [JsonPropertyName("soThuTuThua")] public int? SoThuTuThua { get; set; }
    [JsonPropertyName("maThua")] public string? MaThua { get; set; }
    [JsonPropertyName("dienTich")] public decimal? DienTich { get; set; }
    [JsonPropertyName("diaChi")] public string? DiaChi { get; set; }
    [JsonPropertyName("ListMucDichSuDung")] public List<MucDichSuDungDto>? ListMucDichSuDung { get; set; }
}

// DTO đầy đủ cho Thửa đất (sử dụng trong DangKyQuyenDto)
public class ThuaDatFullInfoDto
{
    [JsonPropertyName("thuaDatId")] public long ThuaDatId { get; set; }
    [JsonPropertyName("xaId")] public int? XaId { get; set; }
    [JsonPropertyName("huyenId")] public int? HuyenId { get; set; }
    [JsonPropertyName("tinhId")] public int? TinhId { get; set; }
    [JsonPropertyName("soHieuToBanDo")] public int? SoHieuToBanDo { get; set; }
    [JsonPropertyName("soThuTuThua")] public int? SoThuTuThua { get; set; }
    [JsonPropertyName("maThua")] public string? MaThua { get; set; }
    [JsonPropertyName("soHieuToBanDoCu")] public string? SoHieuToBanDoCu { get; set; }
    [JsonPropertyName("soThuTuThuaCu")] public string? SoThuTuThuaCu { get; set; }
    [JsonPropertyName("inSoLieuCu")] public bool? InSoLieuCu { get; set; }
    [JsonPropertyName("mucDichSuDungGhep")] public string? MucDichSuDungGhep { get; set; }
    [JsonPropertyName("nguonGocSuDungGhep")] public string? NguonGocSuDungGhep { get; set; }
    [JsonPropertyName("dienTich")] public decimal? DienTich { get; set; }
    [JsonPropertyName("dienTichPhapLy")] public decimal? DienTichPhapLy { get; set; }
    [JsonPropertyName("laDoiTuongChiemDat")] public bool? LaDoiTuongChiemDat { get; set; }
    [JsonPropertyName("quaTrinhSuDung")] public string? QuaTrinhSuDung { get; set; }
    [JsonPropertyName("thoiDiemHinhThanh")] public string? ThoiDiemHinhThanh { get; set; }
    [JsonPropertyName("soHieuToBanDoGoc")] public string? SoHieuToBanDoGoc { get; set; }
    [JsonPropertyName("diaChi")] public string? DiaChi { get; set; }
    [JsonPropertyName("duongDanSoDo")] public string? DuongDanSoDo { get; set; }
    [JsonPropertyName("tenFileSoDo")] public string? TenFileSoDo { get; set; }
    [JsonPropertyName("loaiThuaDat")] public string? LoaiThuaDat { get; set; }
    [JsonPropertyName("lichSuHinhThanh")] public string? LichSuHinhThanh { get; set; }
    [JsonPropertyName("noiDungQuyHoach")] public string? NoiDungQuyHoach { get; set; }
    [JsonPropertyName("ghiChuDienTich")] public string? GhiChuDienTich { get; set; }
    [JsonPropertyName("version")] public int? Version { get; set; }
    [JsonPropertyName("isLastest")] public bool? IsLastest { get; set; }
    [JsonPropertyName("historyId")] public string? HistoryId { get; set; }
    [JsonPropertyName("maThuaDat")] public string? MaThuaDat { get; set; }
    [JsonPropertyName("thuaDatIdOld")] public long? ThuaDatIdOld { get; set; }
    [JsonPropertyName("thuaDatIdGoc")] public long? ThuaDatIdGoc { get; set; }
    [JsonPropertyName("localId")] public long? LocalId { get; set; }
    [JsonPropertyName("isNew")] public bool? IsNew { get; set; }
    [JsonPropertyName("isChange")] public bool? IsChange { get; set; }
    [JsonPropertyName("taiLieuDoDacId")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? TaiLieuDoDacId { get; set; }
    [JsonPropertyName("TaiLieuDoDac")] public object? TaiLieuDoDac { get; set; }
    [JsonPropertyName("ListDiaChi")] public List<DiaChiFullDto>? ListDiaChi { get; set; }
    [JsonPropertyName("ListMucDichSuDung")] public List<MucDichSuDungFullDto>? ListMucDichSuDung { get; set; }
    [JsonPropertyName("ShapeSolrGeometry")] public object? ShapeSolrGeometry { get; set; }
    [JsonPropertyName("geoVN")] public object? GeoVN { get; set; }
    [JsonPropertyName("thuaEditorId")] public string? ThuaEditorId { get; set; }
    [JsonPropertyName("ModifiedDate")] public string? ModifiedDate { get; set; }

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
