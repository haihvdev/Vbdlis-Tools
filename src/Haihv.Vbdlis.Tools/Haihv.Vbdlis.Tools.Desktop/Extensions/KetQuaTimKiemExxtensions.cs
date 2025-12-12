using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;

namespace Haihv.Vbdlis.Tools.Desktop.Extensions;

public static class KetQuaTimKiemExxtensions
{
    /// <summary>
    /// Parse Microsoft JSON Date format like "/Date(1762060782826)/" to DateTime?
    /// </summary>
    private static DateTime? ParseJsonDate(string? jsonDate)
    {
        if (string.IsNullOrWhiteSpace(jsonDate))
            return null;

        // Match pattern: /Date(1234567890123)/
        var match = Regex.Match(jsonDate, @"\/Date\((\d+)\)\/");
        if (match.Success && long.TryParse(match.Groups[1].Value, out var ticks))
        {
            // Convert Unix timestamp (milliseconds) to DateTime
            var date = DateTimeOffset.FromUnixTimeMilliseconds(ticks).DateTime;
            // Chỉ trả về nếu ngày >= 1/1/1900
            return date >= new DateTime(1900, 1, 1) ? date : null;
        }

        // Fallback to standard DateTime parse
        if (DateTime.TryParse(jsonDate, out var parsedDate) && parsedDate >= new DateTime(1900, 1, 1))
            return parsedDate;

        return null;
    }

    /// <summary>
    /// Helper method để lấy thông tin thửa đất từ danh sách liên kết
    /// </summary>
    private static void ProcessThuaDatFromLienKet(
        List<LienKetTaiSanThuaDatDto>? listLienKet,
        HashSet<string> uniqueThuaDat,
        ref ThuaDatModel? firstThuaDat)
    {
        if (listLienKet == null || listLienKet.Count == 0) return;

        foreach (var lienKet in listLienKet)
        {
            if (lienKet?.ThuaDat == null) continue;

            var thuaDat = lienKet.ThuaDat;
            var soToBanDo = thuaDat.SoHieuToBanDo?.ToString() ?? "";
            var soThuaDat = thuaDat.SoThuTuThua?.ToString() ?? "";
            var diaChi = thuaDat.DiaChi ?? "";

            // Tạo key để kiểm tra trùng lặp
            var thuaDatKey = $"{soToBanDo}|{soThuaDat}|{diaChi}";

            if (!uniqueThuaDat.Contains(thuaDatKey))
            {
                uniqueThuaDat.Add(thuaDatKey);

                double? dienTich = null;
                if (thuaDat.DienTich.HasValue)
                {
                    dienTich = (double)thuaDat.DienTich.Value;
                }

                // Lấy mục đích sử dụng từ ListMucDichSuDung
                var mucDichSuDung = thuaDat.ListMucDichSuDung?
                    .FirstOrDefault()?.LoaiMucDichSuDung?.TenLoaiMucDichSuDung
                    ?? thuaDat.MaThua
                    ?? "";

                // Chuyển đổi ListMucDichSuDung từ DTO sang Model
                var listMucDichSuDung = thuaDat.ListMucDichSuDung?
                    .Where(m => !string.IsNullOrWhiteSpace(m.LoaiMucDichSuDungId) && m.DienTich.HasValue && m.DienTich.Value > 0)
                    .Select(m =>
                    {
                        // Chuyển đổi ListNguonGocSuDungDat
                        var listNguonGoc = m.ListNguonGocSuDungDat?
                            .Where(n => n.DienTich.HasValue && n.DienTich.Value > 0)
                            .Select(n => new NguonGocSuDungDatInfo(
                                tenNguonGocChuyenQuyen: n.LoaiNguonGocChuyenQuyen?.TenNguonGocChuyenQuyen ?? "",
                                tenLoaiNguonGocInGiay: n.LoaiNguonGocSuDungDat?.TenLoaiNguonGocInGiay ?? "",
                                dienTich: n.DienTich!.Value
                            ))
                            .ToList();

                        return new MucDichSuDungInfo(
                            loaiMucDichSuDungId: m.LoaiMucDichSuDungId!,
                            dienTich: m.DienTich!.Value,
                            listNguonGocSuDungDat: listNguonGoc
                        );
                    })
                    .ToList();

                var thuaDatModel = new ThuaDatModel(
                    xaId: thuaDat.XaId,
                    soToBanDo: soToBanDo,
                    soThuaDat: soThuaDat,
                    dienTich: dienTich,
                    mucDichSuDung: mucDichSuDung,
                    diaChi: diaChi,
                    listMucDichSuDung: listMucDichSuDung
                );

                // Lưu thửa đất đầu tiên
                firstThuaDat ??= thuaDatModel;
            }
        }
    }

    extension(AdvancedSearchGiayChungNhanResponse giayChungNhanResponse)
    {
        public List<KetQuaTimKiemModel> ToKetQuaTimKiemModels()
        {
            var results = new List<KetQuaTimKiemModel>();

            if (giayChungNhanResponse?.Data == null || giayChungNhanResponse.Data.Count == 0)
            {
                return results;
            }

            foreach (var item in giayChungNhanResponse.Data)
            {
                if (item == null || results.Where(r => r.GiayChungNhanModel.Id == item.GiayChungNhan?.Id).Any() || item.GiayChungNhan == null)
                {
                    continue;
                }

                // Lấy thông tin Giấy chứng nhận
                var giayChungNhan = item.GiayChungNhan;

                var giayChungNhanModel = new GiayChungNhanModel(
                    id: giayChungNhan?.Id ?? "",
                    soPhatHanh: giayChungNhan?.SoPhatHanh ?? "",
                    soVaoSo: giayChungNhan?.SoVaoSo ?? "",
                    ngayVaoSo: ParseJsonDate(giayChungNhan?.NgayVaoSo)
                );

                // Lấy thông tin tất cả chủ sở hữu
                var danhSachChuSoHuu = item.ChuSoHuu != null && item.ChuSoHuu.Count > 0
                    ? string.Join("\n---\n", item.ChuSoHuu.Select(chu =>
                    {
                        var parts = new List<string>();

                        if (!string.IsNullOrWhiteSpace(chu.HoTen))
                        {
                            if (chu.HoTen.Contains("ông", StringComparison.OrdinalIgnoreCase) ||
                                chu.HoTen.Contains("bà", StringComparison.OrdinalIgnoreCase) ||
                                chu.HoTen.Contains("cô", StringComparison.OrdinalIgnoreCase) ||
                                chu.HoTen.Contains("chú", StringComparison.OrdinalIgnoreCase))
                            {
                                // Nếu đã có tiền tố trong họ tên thì không thêm nữa
                                chu.GioiTinh = -1; // Không xác định
                            }
                            var tienTo = chu.GioiTinh == 1 ? "Ông" : (chu.GioiTinh == 0 ? "Bà" : "");
                            var hoTen = !string.IsNullOrWhiteSpace(tienTo) ? $"{tienTo} {chu.HoTen}" : chu.HoTen;
                            parts.Add($"Họ tên: {hoTen}");
                        }
                        if (!string.IsNullOrWhiteSpace(chu.NamSinh))
                            parts.Add($"Năm sinh: {chu.NamSinh}");
                        if (!string.IsNullOrWhiteSpace(chu.SoGiayTo))
                            parts.Add($"Số giấy tờ: {chu.SoGiayTo}");
                        if (!string.IsNullOrWhiteSpace(chu.DiaChi))
                            parts.Add($"Địa chỉ: {chu.DiaChi}");

                        return string.Join("\n", parts);
                    }))
                    : "";

                var chuSuDungModel = new ChuSuDungModel(danhSachChuSoHuu);

                // Lấy thông tin tài sản và thửa đất từ ListDangKyQuyen
                var listDangKyQuyen = giayChungNhan?.ListDangKyQuyen ?? [];

                // Tạo HashSet để theo dõi và loại bỏ trùng lặp
                var uniqueThuaDat = new HashSet<string>();
                var uniqueTaiSan = new HashSet<string>();

                ThuaDatModel? firstThuaDat = null;
                TaiSanModel? firstTaiSan = null;

                foreach (var dangky in listDangKyQuyen)
                {
                    if (dangky == null) continue;

                    // Xử lý Thửa đất (typeItem = 6)
                    if (dangky.ThuaDat != null)
                    {
                        var xaId = dangky.ThuaDat.XaId ?? 0;
                        var soToBanDo = dangky.ThuaDat.SoHieuToBanDo?.ToString() ?? "";
                        var soThuaDat = dangky.ThuaDat.SoThuTuThua?.ToString() ?? "";
                        var diaChi = dangky.ThuaDat.DiaChi ?? "";

                        // Tạo key để kiểm tra trùng lặp
                        var thuaDatKey = $"{soToBanDo}|{soThuaDat}|{diaChi}";

                        if (!uniqueThuaDat.Contains(thuaDatKey))
                        {
                            uniqueThuaDat.Add(thuaDatKey);

                            double? dienTich = null;
                            if (dangky.ThuaDat.DienTich.HasValue)
                            {
                                dienTich = (double)dangky.ThuaDat.DienTich.Value;
                            }

                            // Lấy mục đích sử dụng từ ListMucDichSuDung
                            var mucDichSuDung = dangky.ThuaDat.ListMucDichSuDung?
                                .FirstOrDefault()?.LoaiMucDichSuDung?.TenLoaiMucDichSuDung
                                ?? dangky.ThuaDat.MaThua
                                ?? "";

                            // Chuyển đổi ListMucDichSuDung từ DTO sang Model
                            var listMucDichSuDung = dangky.ThuaDat.ListMucDichSuDung?
                                .Where(m => !string.IsNullOrWhiteSpace(m.LoaiMucDichSuDungId) && m.DienTich.HasValue && m.DienTich.Value > 0)
                                .Select(m =>
                                {
                                    // Chuyển đổi ListNguonGocSuDungDat
                                    var listNguonGoc = m.ListNguonGocSuDungDat?
                                        .Where(n => n.DienTich.HasValue && n.DienTich.Value > 0)
                                        .Select(n => new NguonGocSuDungDatInfo(
                                            tenNguonGocChuyenQuyen: n.LoaiNguonGocChuyenQuyen?.TenNguonGocChuyenQuyen ?? "",
                                            tenLoaiNguonGocInGiay: n.LoaiNguonGocSuDungDat?.TenLoaiNguonGocInGiay ?? "",
                                            dienTich: n.DienTich!.Value
                                        ))
                                        .ToList();

                                    return new MucDichSuDungInfo(
                                        loaiMucDichSuDungId: m.LoaiMucDichSuDungId!,
                                        dienTich: m.DienTich!.Value,
                                        listNguonGocSuDungDat: listNguonGoc
                                    );
                                })
                                .ToList();

                            var thuaDatModel = new ThuaDatModel(
                                xaId: xaId,
                                soToBanDo: soToBanDo,
                                soThuaDat: soThuaDat,
                                dienTich: dienTich,
                                mucDichSuDung: mucDichSuDung,
                                diaChi: diaChi,
                                listMucDichSuDung: listMucDichSuDung
                            );

                            // Lưu thửa đất đầu tiên
                            firstThuaDat ??= thuaDatModel;
                        }
                    }
                    // Xử lý Nhà riêng lẻ (typeItem = 7)
                    else if (dangky.NhaRiengLe != null)
                    {
                        var tenTaiSan = dangky.NhaRiengLe.LoaiNhaRiengLe?.Detail ?? "Nhà ở riêng lẻ";
                        var dienTichXayDung = dangky.NhaRiengLe.DienTichXayDung;
                        var dienTichSuDung = dangky.NhaRiengLe.DienTichSuDung;
                        var soTang = dangky.NhaRiengLe.SoTang ?? "";
                        var diaChi = dangky.NhaRiengLe.ListDiaChi?
                            .FirstOrDefault(d => d.LaDiaChiChinh)?.DiaChiChiTiet
                            ?? dangky.NhaRiengLe.DiaChi
                            ?? "";

                        // Tạo key để kiểm tra trùng lặp
                        var taiSanKey = $"{tenTaiSan}|{dienTichXayDung}|{soTang}|{diaChi}";

                        if (!uniqueTaiSan.Contains(taiSanKey))
                        {
                            uniqueTaiSan.Add(taiSanKey);

                            var taiSanModel = new TaiSanModel(
                                tenTaiSan: tenTaiSan,
                                dienTichXayDung: dienTichXayDung,
                                dienTichSuDung: dienTichSuDung,
                                soTang: soTang,
                                diaChi: diaChi
                            );

                            // Lưu tài sản đầu tiên
                            firstTaiSan ??= taiSanModel;
                        }

                        // Lấy thông tin thửa đất từ liên kết (nếu chưa có thửa đất)
                        ProcessThuaDatFromLienKet(dangky.NhaRiengLe.ListLienKetTaiSanThuaDat, uniqueThuaDat, ref firstThuaDat);
                    }
                    // Xử lý Căn hộ (typeItem = 8)
                    else if (dangky.CanHo != null)
                    {
                        // Tạo tên tài sản: "Căn hộ soHieuCanHo (tenChungCu)" hoặc "Căn hộ tenCanHo (tenChungCu)"
                        var soHieuOrTen = dangky.CanHo.SoHieuCanHo ?? dangky.CanHo.TenCanHo ?? "";
                        var tenChungCu = dangky.CanHo.NhaChungCu?.TenChungCu;

                        string tenTaiSan;
                        if (!string.IsNullOrWhiteSpace(tenChungCu))
                        {
                            tenTaiSan = $"Căn hộ {soHieuOrTen} ({tenChungCu})".Trim();
                        }
                        else
                        {
                            tenTaiSan = $"Căn hộ {soHieuOrTen}".Trim();
                        }
                        var dienTichXayDung = dangky.CanHo.DienTichSan;
                        var dienTichSuDung = dangky.CanHo.DienTichSuDung;
                        var soTang = dangky.CanHo.TangSo ?? "";
                        var diaChi = dangky.CanHo.DiaChiCanHo ?? "";

                        // Tạo key để kiểm tra trùng lặp
                        var taiSanKey = $"{tenTaiSan}|{dienTichXayDung}|{soTang}|{diaChi}";

                        if (!uniqueTaiSan.Contains(taiSanKey))
                        {
                            uniqueTaiSan.Add(taiSanKey);

                            var taiSanModel = new TaiSanModel(
                                tenTaiSan: tenTaiSan,
                                dienTichXayDung: dienTichXayDung,
                                dienTichSuDung: dienTichSuDung,
                                soTang: soTang,
                                diaChi: diaChi
                            );

                            // Lưu tài sản đầu tiên
                            firstTaiSan ??= taiSanModel;
                        }

                        // Lấy thông tin thửa đất từ liên kết
                        // Ưu tiên 1: CanHo.ListLienKetTaiSanThuaDat
                        if (dangky.CanHo.ListLienKetTaiSanThuaDat != null && dangky.CanHo.ListLienKetTaiSanThuaDat.Count > 0)
                        {
                            ProcessThuaDatFromLienKet(dangky.CanHo.ListLienKetTaiSanThuaDat, uniqueThuaDat, ref firstThuaDat);
                        }
                        // Ưu tiên 2: NhaChungCu.ListThuaLienKet (nếu không có trong CanHo)
                        else if (dangky.CanHo.NhaChungCu?.ListThuaLienKet != null)
                        {
                            ProcessThuaDatFromLienKet(dangky.CanHo.NhaChungCu.ListThuaLienKet, uniqueThuaDat, ref firstThuaDat);
                        }
                    }
                }

                results.Add(new KetQuaTimKiemModel(
                    ChuSuDung: chuSuDungModel,
                    GiayChungNhanModel: giayChungNhanModel,
                    ThuaDatModel: firstThuaDat,
                    TaiSan: firstTaiSan
                ));
            }

            return results;
        }
    }
}
