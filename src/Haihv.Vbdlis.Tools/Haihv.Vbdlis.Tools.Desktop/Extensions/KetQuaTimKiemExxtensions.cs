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
        List<ThuaDatModel> allThuaDat)
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

                // Thêm vào danh sách tất cả thửa đất
                allThuaDat.Add(thuaDatModel);
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

                // Lấy thông tin tài sản và thửa đất từ ListDangKyQuyen
                var listDangKyQuyen = giayChungNhan?.ListDangKyQuyen ?? [];

                // Thu thập tất cả chủ sử dụng (bỏ trùng theo ID)
                var allChuSuDung = new List<ChuSuDungModel>();

                // Thu thập tất cả CaNhan (bỏ trùng theo CaNhanId)
                var allCaNhan = listDangKyQuyen
                    .Where(d => d.CaNhan != null)
                    .Select(d => d.CaNhan!)
                    .GroupBy(c => c.CaNhanId)
                    .Select(g => g.First());

                foreach (var caNhan in allCaNhan)
                {
                    allChuSuDung.Add(new ChuSuDungModel { CaNhan = caNhan });
                }

                // Thu thập tất cả VoChong (bỏ trùng theo VoChongId)
                var allVoChong = listDangKyQuyen
                    .Where(d => d.VoChong != null)
                    .Select(d => d.VoChong!)
                    .GroupBy(v => v.VoChongId)
                    .Select(g => g.First());

                foreach (var voChong in allVoChong)
                {
                    allChuSuDung.Add(new ChuSuDungModel { VoChong = voChong });
                }

                // Thu thập tất cả HoGiaDinh (bỏ trùng theo HoGiaDinhId)
                var allHoGiaDinh = listDangKyQuyen
                    .Where(d => d.HoGiaDinh != null)
                    .Select(d => d.HoGiaDinh!)
                    .GroupBy(h => h.HoGiaDinhId)
                    .Select(g => g.First());

                foreach (var hoGiaDinh in allHoGiaDinh)
                {
                    allChuSuDung.Add(new ChuSuDungModel { HoGiaDinh = hoGiaDinh });
                }

                // Thu thập tất cả ToChuc (bỏ trùng theo ToChucId)
                var allToChuc = listDangKyQuyen
                    .Where(d => d.ToChuc != null)
                    .Select(d => d.ToChuc!)
                    .GroupBy(t => t.ToChucId)
                    .Select(g => g.First());

                foreach (var toChuc in allToChuc)
                {
                    allChuSuDung.Add(new ChuSuDungModel { ToChuc = toChuc });
                }

                // Thu thập CongDong (bỏ trùng)
                var allCongDong = listDangKyQuyen
                    .Where(d => d.CongDong != null)
                    .Select(d => d.CongDong?.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct();

                foreach (var congDong in allCongDong)
                {
                    allChuSuDung.Add(new ChuSuDungModel { CongDong = congDong });
                }

                // Thu thập NhomNguoi (bỏ trùng)
                var allNhomNguoi = listDangKyQuyen
                    .Where(d => d.NhomNguoi != null)
                    .Select(d => d.NhomNguoi?.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct();

                foreach (var nhomNguoi in allNhomNguoi)
                {
                    allChuSuDung.Add(new ChuSuDungModel { NhomNguoi = nhomNguoi });
                }

                // Nếu không có chủ sử dụng nào, tạo một ChuSuDungModel trống
                if (allChuSuDung.Count == 0)
                {
                    allChuSuDung.Add(new ChuSuDungModel());
                }

                // Tạo HashSet để theo dõi và loại bỏ trùng lặp
                var uniqueThuaDat = new HashSet<string>();
                var uniqueTaiSan = new HashSet<string>();

                // Tạo danh sách để lưu tất cả thửa đất và tài sản
                var allThuaDat = new List<ThuaDatModel>();
                var allTaiSan = new List<TaiSanModel>();

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

                            // Thêm vào danh sách tất cả thửa đất
                            allThuaDat.Add(thuaDatModel);
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

                            // Thêm vào danh sách tất cả tài sản
                            allTaiSan.Add(taiSanModel);
                        }

                        // Lấy thông tin thửa đất từ liên kết
                        ProcessThuaDatFromLienKet(dangky.NhaRiengLe.ListLienKetTaiSanThuaDat, uniqueThuaDat, allThuaDat);
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

                            // Thêm vào danh sách tất cả tài sản
                            allTaiSan.Add(taiSanModel);
                        }

                        // Lấy thông tin thửa đất từ liên kết
                        // Ưu tiên 1: CanHo.ListLienKetTaiSanThuaDat
                        if (dangky.CanHo.ListLienKetTaiSanThuaDat != null && dangky.CanHo.ListLienKetTaiSanThuaDat.Count > 0)
                        {
                            ProcessThuaDatFromLienKet(dangky.CanHo.ListLienKetTaiSanThuaDat, uniqueThuaDat, allThuaDat);
                        }
                        // Ưu tiên 2: NhaChungCu.ListThuaLienKet (nếu không có trong CanHo)
                        else if (dangky.CanHo.NhaChungCu?.ListThuaLienKet != null)
                        {
                            ProcessThuaDatFromLienKet(dangky.CanHo.NhaChungCu.ListThuaLienKet, uniqueThuaDat, allThuaDat);
                        }
                    }
                }

                // Tạo một KetQuaTimKiemModel duy nhất với tất cả chủ sử dụng, thửa đất và tài sản
                results.Add(new KetQuaTimKiemModel(
                    ListChuSuDung: allChuSuDung,
                    GiayChungNhanModel: giayChungNhanModel,
                    ListThuaDat: allThuaDat,
                    ListTaiSan: allTaiSan
                ));
            }

            return results;
        }
    }
}
