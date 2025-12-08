using System;

namespace Haihv.Vbdlis.Tools.Desktop.Models;

/// <summary>
/// Thông tin Giấy chứng nhận tổng hợp
/// </summary>
public record KetQuaTimKiemModel(ChuSuDungModel ChuSuDung, GiayChungNhanModel GiayChungNhanModel, ThuaDatModel ThuaDatModel, TaiSanModel TaiSan);

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


public class ThuaDatModel(string soToBanDo, string soThuaDat, double? dienTich, string mucDichSuDung, string diaChi)
{
    public string SoToBanDo { get; } = soToBanDo;
    public string SoThuaDat { get; } = soThuaDat;
    public double? DienTich { get; } = dienTich;
    public string MucDichSuDung { get; } = mucDichSuDung;
    public string DiaChi { get; } = diaChi;

    public string DienTichFormatted => DienTich.HasValue && DienTich.Value > 0
        ? $"{DienTich.Value} m²"
        : "";

    public bool HasDienTich => DienTich.HasValue && DienTich.Value > 0;
}

public class TaiSanModel(string loaiTaiSan, double? dienTichXayDung, double? dienTichSuDung, string soTang, string diaChi)
{
    public string LoaiTaiSan { get; } = loaiTaiSan;
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
