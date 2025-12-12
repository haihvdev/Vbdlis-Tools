using System;
using System.Collections.Generic;
using System.Linq;

namespace Haihv.Vbdlis.Tools.Desktop.Models;

/// <summary>
/// Thông tin Giấy chứng nhận tổng hợp
/// </summary>
public record KetQuaTimKiemModel(ChuSuDungModel ChuSuDung, GiayChungNhanModel GiayChungNhanModel, ThuaDatModel? ThuaDatModel, TaiSanModel? TaiSan);

public record ChuSuDungModel(string DanhSachChuSoHuu);

public class GiayChungNhanModel(string id, string soPhatHanh, string soVaoSo, DateTime? ngayVaoSo)
{
    public string Id { get; } = id;
    public string SoPhatHanh { get; } = soPhatHanh;
    public string SoVaoSo { get; } = soVaoSo;
    public DateTime? NgayVaoSo { get; } = ngayVaoSo;

    public string NgayVaoSoFormatted => NgayVaoSo.HasValue && NgayVaoSo.Value >= new DateTime(1900, 1, 1)
        ? NgayVaoSo.Value.ToString("dd/MM/yyyy")
        : "";

    public bool HasNgayVaoSo => NgayVaoSo.HasValue && NgayVaoSo.Value >= new DateTime(1900, 1, 1);
}


public class ThuaDatModel(int xaId, string soToBanDo, string soThuaDat, double? dienTich, string mucDichSuDung, string diaChi, List<MucDichSuDungInfo>? listMucDichSuDung)
{
    public int XaId { get; set; } = xaId;
    public string SoToBanDo { get; } = soToBanDo;
    public string SoThuaDat { get; } = soThuaDat;
    public double? DienTich { get; } = dienTich;
    public string MucDichSuDung { get; } = mucDichSuDung;

    public string DiaChi
    { get; } = diaChi;
    public List<MucDichSuDungInfo> ListMucDichSuDung { get; } = listMucDichSuDung ?? [];

    public string DienTichFormatted => DienTich.HasValue && DienTich.Value > 0
        ? $"{DienTich.Value} m²"
        : "";

    public bool HasDienTich => DienTich.HasValue && DienTich.Value > 0;

    public string MucDichSuDungFormatted
    {
        get
        {
            if (ListMucDichSuDung == null || ListMucDichSuDung.Count == 0)
                return MucDichSuDung;

            var parts = ListMucDichSuDung
                .Where(m => !string.IsNullOrWhiteSpace(m.LoaiMucDichSuDungId) && m.DienTich > 0)
                .Select(m => $"{m.LoaiMucDichSuDungId} ({m.DienTich} m²)");

            return string.Join(", ", parts);
        }
    }

    public bool HasMucDichSuDung => ListMucDichSuDung != null && ListMucDichSuDung.Count > 0;

    public string NguonGocSuDungDatFormatted
    {
        get
        {
            if (ListMucDichSuDung == null || ListMucDichSuDung.Count == 0)
                return "";

            var allNguonGoc = ListMucDichSuDung
                .SelectMany(m => m.ListNguonGocSuDungDat)
                .Where(n => !string.IsNullOrWhiteSpace(n.TenLoaiNguonGocInGiay) && n.DienTich > 0)
                .Select(n =>
                {
                    var prefix = string.IsNullOrWhiteSpace(n.TenNguonGocChuyenQuyen?.Trim())
                        ? ""
                        : $"{n.TenNguonGocChuyenQuyen.Trim()} ";
                    return $"{prefix}{n.TenLoaiNguonGocInGiay} ({n.DienTich} m²)";
                });

            return string.Join(", ", allNguonGoc);
        }
    }

    public bool HasNguonGocSuDungDat =>
        ListMucDichSuDung != null &&
        ListMucDichSuDung.Any(m => m.ListNguonGocSuDungDat != null && m.ListNguonGocSuDungDat.Count > 0);
}

public class MucDichSuDungInfo(string loaiMucDichSuDungId, double dienTich, List<NguonGocSuDungDatInfo>? listNguonGocSuDungDat = null)
{
    public string LoaiMucDichSuDungId { get; } = loaiMucDichSuDungId;
    public double DienTich { get; } = dienTich;
    public List<NguonGocSuDungDatInfo> ListNguonGocSuDungDat { get; } = listNguonGocSuDungDat ?? [];
}

public class NguonGocSuDungDatInfo(string tenNguonGocChuyenQuyen, string tenLoaiNguonGocInGiay, double dienTich)
{
    public string TenNguonGocChuyenQuyen { get; } = tenNguonGocChuyenQuyen;
    public string TenLoaiNguonGocInGiay { get; } = tenLoaiNguonGocInGiay;
    public double DienTich { get; } = dienTich;
}

public class TaiSanModel(string tenTaiSan, double? dienTichXayDung, double? dienTichSuDung, string soTang, string diaChi)
{
    public string TenTaiSan { get; } = tenTaiSan;
    public double? DienTichXayDung { get; } = dienTichXayDung;
    public double? DienTichSuDung { get; } = dienTichSuDung;
    public string SoTang { get; } = soTang;
    public string DiaChi { get; } = diaChi;

    public string DienTichXayDungFormatted => DienTichXayDung.HasValue && DienTichXayDung.Value > 0
        ? $"{DienTichXayDung.Value} m²"
        : "";

    public bool HasDienTichXayDung => DienTichXayDung.HasValue && DienTichXayDung.Value > 0;

    public string DienTichSuDungFormatted => DienTichSuDung.HasValue && DienTichSuDung.Value > 0
        ? $"{DienTichSuDung.Value} m²"
        : "";

    public bool HasDienTichSuDung => DienTichSuDung.HasValue && DienTichSuDung.Value > 0;
}
