using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Haihv.Vbdlis.Tools.Desktop.Models;

/// <summary>
/// Thông tin Giấy chứng nhận tổng hợp
/// </summary>
public record KetQuaTimKiemModel(
    List<ChuSuDungModel> ListChuSuDung,
    GiayChungNhanModel GiayChungNhanModel,
    List<ThuaDatModel> ListThuaDat,
    List<TaiSanModel> ListTaiSan)
{
    /// <summary>
    /// Format tất cả chủ sử dụng với dấu phân cách ---
    /// </summary>
    public string ChuSuDungCompact
    {
        get
        {
            if (ListChuSuDung == null || ListChuSuDung.Count == 0)
                return string.Empty;

            var allParts = ListChuSuDung
                .Select(chu => chu.ChuSuDungCompact)
                .Where(s => !string.IsNullOrWhiteSpace(s));

            return string.Join($"{Environment.NewLine}---{Environment.NewLine}", allParts);
        }
    }

    /// <summary>
    /// Format tất cả thửa đất với dấu phân cách ---
    /// </summary>
    public string ThuaDatCompact
    {
        get
        {
            if (ListThuaDat == null || ListThuaDat.Count == 0)
                return string.Empty;

            var allParts = ListThuaDat
                .Select(td => td.ThuaDatCompact)
                .Where(s => !string.IsNullOrWhiteSpace(s));

            return string.Join($"{Environment.NewLine}---{Environment.NewLine}", allParts);
        }
    }

    /// <summary>
    /// Format tất cả tài sản với dấu phân cách ---
    /// </summary>
    public string TaiSanCompact
    {
        get
        {
            if (ListTaiSan == null || ListTaiSan.Count == 0)
                return string.Empty;

            var allParts = ListTaiSan
                .Select(ts => ts.TaiSanCompact)
                .Where(s => !string.IsNullOrWhiteSpace(s));

            return string.Join($"{Environment.NewLine}---{Environment.NewLine}", allParts);
        }
    }
}

public class ChuSuDungModel
{
    public CaNhanFullDto? CaNhan { get; set; }
    public VoChongDto? VoChong { get; set; }
    public HoGiaDinhFullDto? HoGiaDinh { get; set; }
    public ToChucDto? ToChuc { get; set; }
    public string? CongDong { get; set; }
    public string? NhomNguoi { get; set; }

    private static string? FormatSoGiayTo(IEnumerable<GiayToTuyThanDto>? giayToList, bool showDashIfEmpty)
    {
        if (giayToList == null)
            return showDashIfEmpty ? "(-)" : null;

        var allNumbers = giayToList
            .Where(g => !string.IsNullOrWhiteSpace(g.SoGiayTo))
            .OrderByDescending(g => g.LaThongTinChinh == true)
            .Select(g => g.SoGiayTo!.Trim())
            .ToList();

        if (allNumbers.Count == 0)
            return showDashIfEmpty ? "(-)" : null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinctNumbers = new List<string>();
        foreach (var number in allNumbers)
        {
            if (seen.Add(number))
                distinctNumbers.Add(number);
        }

        var first = distinctNumbers[0];
        return distinctNumbers.Count == 1
            ? first
            : first + string.Concat(distinctNumbers.Skip(1).Select(n => $"; ({n})"));
    }

    private static string? FormatSoGiayTo(IEnumerable<GiayToToChucDto>? giayToToChucList, bool showDashIfEmpty)
    {
        if (giayToToChucList == null)
            return showDashIfEmpty ? "(-)" : null;

        var grouped = new Dictionary<string, List<GiayToToChucDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var giayTo in giayToToChucList)
        {
            if (string.IsNullOrWhiteSpace(giayTo.SoGiayTo))
                continue;

            var key = (GetLoaiGiayToToChucText(giayTo) ?? "").Trim();
            if (!grouped.TryGetValue(key, out var list))
            {
                list = [];
                grouped[key] = list;
            }

            list.Add(giayTo);
        }

        if (grouped.Count == 0)
            return showDashIfEmpty ? "(-)" : null;

        var parts = new List<string>();
        foreach (var group in grouped)
        {
            var label = string.IsNullOrWhiteSpace(group.Key) ? "" : group.Key;
            var numbers = FormatSoGiayToNumbers(group.Value.Select(g => g.SoGiayTo), showDashIfEmpty: false);
            if (!string.IsNullOrWhiteSpace(numbers))
                parts.Add($"{label}: {numbers}");
        }

        if (parts.Count == 0)
            return showDashIfEmpty ? "(-)" : null;

        return string.Join("; ", parts);
    }

    private static string? GetLoaiGiayToToChucText(GiayToToChucDto giayTo)
    {
        var value = giayTo.LoaiGiayToToChuc;
        if (value == null)
            return null;

        if (value is string str)
            return str;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return element.GetString();

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "tenLoaiGiayToToChuc", "TenLoaiGiayToToChuc", "tenLoaiGiayTo", "TenLoaiGiayTo", "ten", "Ten", "name", "Name", "title", "Title" })
                {
                    if (TryGetJsonString(element, key, out var text))
                        return text;
                }
            }

            return null;
        }

        if (value is IDictionary<string, object?> dict)
        {
            foreach (var key in new[] { "tenLoaiGiayToToChuc", "TenLoaiGiayToToChuc", "tenLoaiGiayTo", "TenLoaiGiayTo", "ten", "Ten", "name", "Name", "title", "Title" })
            {
                if (!dict.TryGetValue(key, out var raw) || raw == null)
                    continue;

                if (raw is string text && !string.IsNullOrWhiteSpace(text))
                    return text;

                if (raw is JsonElement rawElement && rawElement.ValueKind == JsonValueKind.String)
                    return rawElement.GetString();
            }
        }

        return null;
    }

    private static bool TryGetJsonString(JsonElement obj, string propertyName, out string? value)
    {
        value = null;

        if (!obj.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? FormatSoGiayToNumbers(IEnumerable<string?>? soGiayToList, bool showDashIfEmpty)
    {
        if (soGiayToList == null)
            return showDashIfEmpty ? "(-)" : null;

        var allNumbers = soGiayToList
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToList();

        if (allNumbers.Count == 0)
            return showDashIfEmpty ? "(-)" : null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinctNumbers = new List<string>();
        foreach (var number in allNumbers)
        {
            if (seen.Add(number))
                distinctNumbers.Add(number);
        }

        var first = distinctNumbers[0];
        return distinctNumbers.Count == 1
            ? first
            : first + string.Concat(distinctNumbers.Skip(1).Select(n => $"; ({n})"));
    }

    public string TenChu(bool includeTitle = true)
    {
        static string OngBa(bool? gioiTinh, bool capitalize)
        {
            if (gioiTinh == true)
                return capitalize ? "Ông" : "ông";

            return capitalize ? "Bà" : "bà";
        }

        var parts = new List<string>();

        if (CaNhan != null && !string.IsNullOrWhiteSpace(CaNhan.HoTen))
            parts.Add($"{(includeTitle ? "Họ tên: " : "")}{OngBa(CaNhan.GioiTinh, capitalize: !includeTitle)} {CaNhan.HoTen}");

        if (VoChong != null)
        {
            var tenChong = VoChong.Chong?.HoTen ?? "";
            var tenVo = VoChong.Vo?.HoTen ?? "";

            if (!string.IsNullOrWhiteSpace(tenChong) && !string.IsNullOrWhiteSpace(tenVo))
            {
                var tenVoChong = VoChong.InVoTruoc == true
                    ? $"{(includeTitle ? "Họ tên: " : "")}vợ {tenVo} và chồng {tenChong}"
                    : $"{(includeTitle ? "Họ tên: " : "")}chồng {tenChong} và vợ {tenVo}";
                parts.Add(tenVoChong);
            }
            else if (!string.IsNullOrWhiteSpace(tenChong))
                parts.Add($"{(includeTitle ? "Họ tên: " : "")}{OngBa(true, capitalize: !includeTitle)} {tenChong}");
            else if (!string.IsNullOrWhiteSpace(tenVo))
                parts.Add($"{(includeTitle ? "Họ tên: " : "")}{OngBa(false, capitalize: !includeTitle)} {tenVo}");
        }

        if (HoGiaDinh != null)
        {
            var tenThanhVien = new List<string>();

            var tenChuHo = HoGiaDinh.ChuHo?.HoTen ?? "";
            if (!string.IsNullOrWhiteSpace(tenChuHo))
                tenThanhVien.Add($"Chủ hộ {(HoGiaDinh.ChuHo?.GioiTinh == true ? "Ông" : "Bà")} {tenChuHo}");

            if (HoGiaDinh.ListThanhVienHoGiaDinh != null && HoGiaDinh.ListThanhVienHoGiaDinh.Count > 0)
            {
                var tenCacThanhVien = HoGiaDinh.ListThanhVienHoGiaDinh
                    .Where(tv => !string.IsNullOrWhiteSpace(tv.HoTen))
                    .Select(tv => $"{OngBa(tv.GioiTinh, capitalize: !includeTitle)} {tv.HoTen!}");
                tenThanhVien.AddRange(tenCacThanhVien);
            }

            var tenHoGiaDinh = tenThanhVien.Count > 0
                ? $"{(includeTitle ? "Hộ gia đình: " : "")}({string.Join(", ", tenThanhVien)})"
                : "Hộ gia đình: (không tìm thấy thành viên)";
            parts.Add(tenHoGiaDinh);
        }

        if (ToChuc != null && !string.IsNullOrWhiteSpace(ToChuc.TenToChuc))
            parts.Add($"{(includeTitle ? "Tên tổ chức: " : "")}{ToChuc.TenToChuc}");

        if (CongDong != null && !string.IsNullOrWhiteSpace(CongDong))
            parts.Add($"{(includeTitle ? "Cộng đồng: " : "")}{CongDong}");

        if (NhomNguoi != null && !string.IsNullOrWhiteSpace(NhomNguoi))
            parts.Add($"{(includeTitle ? "Nhóm người: " : "")}{NhomNguoi}");

        return string.Join("; ", parts);
    }

    public string? NamSinh
    {
        get
        {
            var parts = new List<string>();

            if (CaNhan?.NamSinh != null)
                parts.Add(CaNhan.NamSinh.ToString()!);

            if (VoChong != null)
            {
                var namSinhChong = VoChong.Chong?.NamSinh?.ToString() ?? "";
                var namSinhVo = VoChong.Vo?.NamSinh?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(namSinhChong) && !string.IsNullOrWhiteSpace(namSinhVo))
                {
                    var namSinhVoChong = VoChong.InVoTruoc == true
                        ? $"{namSinhVo}, {namSinhChong}"
                        : $"{namSinhChong}, {namSinhVo}";
                    parts.Add(namSinhVoChong);
                }
                else if (!string.IsNullOrWhiteSpace(namSinhChong))
                    parts.Add(namSinhChong);
                else if (!string.IsNullOrWhiteSpace(namSinhVo))
                    parts.Add(namSinhVo);
            }

            if (HoGiaDinh != null)
            {
                var namSinhThanhVien = new List<string>();

                if (HoGiaDinh.ChuHo?.NamSinh != null)
                    namSinhThanhVien.Add($"Chủ hộ: {HoGiaDinh.ChuHo.NamSinh}");

                if (HoGiaDinh.ListThanhVienHoGiaDinh != null && HoGiaDinh.ListThanhVienHoGiaDinh.Count > 0)
                {
                    var namSinhCacThanhVien = HoGiaDinh.ListThanhVienHoGiaDinh
                        .Where(tv => tv.NamSinh != null)
                        .Select(tv => tv.NamSinh!.Value.ToString());
                    namSinhThanhVien.AddRange(namSinhCacThanhVien);
                }

                if (namSinhThanhVien.Count > 0)
                    parts.Add(string.Join(", ", namSinhThanhVien));
            }

            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }
    }

    public string? SoGiayTo
    {
        get
        {
            var parts = new List<string>();

            if (CaNhan != null)
            {
                parts.Add(FormatSoGiayTo(CaNhan.ListGiayToTuyThan, showDashIfEmpty: true)!);
            }

            if (VoChong != null)
            {
                var giayToChong = FormatSoGiayTo(VoChong.Chong?.ListGiayToTuyThan, showDashIfEmpty: false);
                var giayToVo = FormatSoGiayTo(VoChong.Vo?.ListGiayToTuyThan, showDashIfEmpty: false);

                if (!string.IsNullOrWhiteSpace(giayToChong) && !string.IsNullOrWhiteSpace(giayToVo))
                {
                    var giayToVoChong = VoChong.InVoTruoc == true
                        ? $"{giayToVo}, {giayToChong}"
                        : $"{giayToChong}, {giayToVo}";
                    parts.Add(giayToVoChong);
                }
                else if (!string.IsNullOrWhiteSpace(giayToChong))
                    parts.Add(giayToChong);
                else if (!string.IsNullOrWhiteSpace(giayToVo))
                    parts.Add(giayToVo);
                else
                    parts.Add("(-)");
            }

            if (HoGiaDinh != null)
            {
                var giayToThanhVien = new List<string>();

                var chuHoGiayTo = FormatSoGiayTo(HoGiaDinh.ChuHo?.ListGiayToTuyThan, showDashIfEmpty: true);
                giayToThanhVien.Add($"Chủ hộ: {chuHoGiayTo}");

                if (HoGiaDinh.ListThanhVienHoGiaDinh != null && HoGiaDinh.ListThanhVienHoGiaDinh.Count > 0)
                {
                    foreach (var tv in HoGiaDinh.ListThanhVienHoGiaDinh)
                    {
                        var tvGiayTo = FormatSoGiayTo(tv.ListGiayToTuyThan, showDashIfEmpty: false);
                        if (!string.IsNullOrWhiteSpace(tvGiayTo))
                            giayToThanhVien.Add(tvGiayTo);
                    }
                }

                if (giayToThanhVien.Count > 0)
                    parts.Add(string.Join(", ", giayToThanhVien));
            }

            if (ToChuc != null)
            {
                var giayToToChuc = FormatSoGiayTo(ToChuc.ListGiayToBoSung, showDashIfEmpty: false);
                if (!string.IsNullOrWhiteSpace(giayToToChuc))
                    parts.Add(giayToToChuc);
            }

            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }
    }

    public string? DiaChi
    {
        get
        {
            var parts = new List<string>();

            if (CaNhan != null)
            {
                if (!string.IsNullOrWhiteSpace(CaNhan.DiaChi))
                    parts.Add(CaNhan.DiaChi);
                else
                {
                    var diaChiCaNhan = CaNhan.ListDiaChi?
                        .FirstOrDefault(d => d.LaDiaChiChinh)?.DiaChiChiTiet
                        ?? CaNhan.DiaChi;
                    if (!string.IsNullOrWhiteSpace(diaChiCaNhan))
                        parts.Add(diaChiCaNhan);
                }
            }

            if (VoChong != null)
            {
                var diaChiChong = string.IsNullOrWhiteSpace(VoChong.Chong?.DiaChi) ?
                    VoChong.Chong?.ListDiaChi?
                        .FirstOrDefault(d => d.LaDiaChiChinh)?.DiaChiChiTiet
                        ?? VoChong.Chong?.DiaChi ?? ""
                    : VoChong.Chong?.DiaChi ?? "";
                var diaChiVo = string.IsNullOrWhiteSpace(VoChong.Vo?.DiaChi) ?
                    VoChong.Vo?.ListDiaChi?
                    .FirstOrDefault(d => d.LaDiaChiChinh)?.DiaChiChiTiet
                    ?? VoChong.Vo?.DiaChi ?? ""
                    : VoChong.Vo?.DiaChi ?? "";

                // Nếu cả hai có địa chỉ giống nhau, chỉ thêm một
                if (!string.IsNullOrWhiteSpace(diaChiChong) && diaChiChong == diaChiVo)
                {
                    parts.Add(diaChiChong);
                }
                else if (!string.IsNullOrWhiteSpace(diaChiChong) && !string.IsNullOrWhiteSpace(diaChiVo))
                {
                    var diaChiVoChong = VoChong.InVoTruoc == true
                        ? $"{diaChiVo}; {diaChiChong}"
                        : $"{diaChiChong}; {diaChiVo}";
                    parts.Add(diaChiVoChong);
                }
                else if (!string.IsNullOrWhiteSpace(diaChiChong))
                    parts.Add(diaChiChong);
                else if (!string.IsNullOrWhiteSpace(diaChiVo))
                    parts.Add(diaChiVo);
            }

            if (HoGiaDinh != null)
            {

                // Địa chỉ hộ gia đình thường chung, nên chỉ lấy địa chỉ chung của hộ
                var diaChiHoGiaDinh = string.IsNullOrWhiteSpace(HoGiaDinh.DiaChi) ?
                    HoGiaDinh.ListDiaChi?
                        .FirstOrDefault(d => d.LaDiaChiChinh)?.DiaChiChiTiet
                        ?? HoGiaDinh.DiaChi
                    : HoGiaDinh.DiaChi;
                if (!string.IsNullOrWhiteSpace(diaChiHoGiaDinh))
                    parts.Add(diaChiHoGiaDinh);
            }

            if (ToChuc != null)
            {
                var diaChiToChuc = string.IsNullOrWhiteSpace(ToChuc.DiaChi) ?
                    ToChuc.ListDiaChi?
                        .FirstOrDefault(d => d.LaDiaChiChinh)?.DiaChiChiTiet
                        ?? ToChuc.DiaChi
                    : ToChuc.DiaChi;
                if (!string.IsNullOrWhiteSpace(diaChiToChuc))
                    parts.Add(diaChiToChuc);
            }

            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }
    }
    public string ChuSuDungCompact
    {
        get
        {
            var parts = new List<string>();
            var tenChu = TenChu();
            if (!string.IsNullOrWhiteSpace(tenChu))
                parts.Add(tenChu);

            if (!string.IsNullOrWhiteSpace(NamSinh))
                parts.Add($"Năm sinh: {NamSinh}");

            if (!string.IsNullOrWhiteSpace(SoGiayTo))
                parts.Add($"Số giấy tờ: {SoGiayTo}");

            if (!string.IsNullOrWhiteSpace(DiaChi))
                parts.Add($"Địa chỉ: {DiaChi}");

            return string.Join(Environment.NewLine, parts);
        }
    }
}

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

    public string ThuaDatCompact
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(SoToBanDo))
                parts.Add($"Tờ bản đồ số: {SoToBanDo}");

            if (!string.IsNullOrWhiteSpace(SoThuaDat))
                parts.Add($"Thửa đất số: {SoThuaDat}");

            if (HasDienTich)
                parts.Add($"Diện tích: {DienTichFormatted}");

            if (HasMucDichSuDung)
                parts.Add($"Mục đích sử dụng: {MucDichSuDungFormatted}");
            else if (!string.IsNullOrWhiteSpace(MucDichSuDung))
                parts.Add($"Mục đích sử dụng: {MucDichSuDung}");

            if (HasNguonGocSuDungDat)
                parts.Add($"Nguồn gốc sử dụng đất: {NguonGocSuDungDatFormatted}");

            if (!string.IsNullOrWhiteSpace(DiaChi))
                parts.Add($"Địa chỉ: {DiaChi}");

            return string.Join(Environment.NewLine, parts);
        }
    }
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

    public string TaiSanCompact
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(TenTaiSan))
                parts.Add($"Tên tài sản: {TenTaiSan}");

            if (HasDienTichXayDung)
                parts.Add($"Diện tích xây dựng: {DienTichXayDungFormatted}");

            if (HasDienTichSuDung)
                parts.Add($"Diện tích sử dụng: {DienTichSuDungFormatted}");

            if (!string.IsNullOrWhiteSpace(SoTang))
                parts.Add($"Số tầng: {SoTang}");

            if (!string.IsNullOrWhiteSpace(DiaChi))
                parts.Add($"Địa chỉ: {DiaChi}");

            return string.Join(Environment.NewLine, parts);
        }
    }
}
