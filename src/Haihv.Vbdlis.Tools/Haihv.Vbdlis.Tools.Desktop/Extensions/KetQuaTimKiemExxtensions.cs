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
                if (item == null || results.Where(r => r.GiayChungNhanModel.Id == item.GiayChungNhan?.Id).Any())
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

                // Lấy thông tin tài sản
                var taiSan = item.TaiSan?.FirstOrDefault();
                var thuaDatModel = new ThuaDatModel(
                    soToBanDo: taiSan?.SoHieuToBanDo?.ToString() ?? "",
                    soThuaDat: taiSan?.SoThuTuThua?.ToString() ?? "",
                    dienTich: null,
                    mucDichSuDung: "",
                    diaChi: taiSan?.DiaChi ?? ""
                );

                var taiSanModel = new TaiSanModel(
                    loaiTaiSan: "",
                    dienTichXayDung: null,
                    dienTichSuDung: null,
                    soTang: "",
                    diaChi: taiSan?.DiaChi ?? ""
                );

                results.Add(new KetQuaTimKiemModel(
                    ChuSuDung: chuSuDungModel,
                    GiayChungNhanModel: giayChungNhanModel,
                    ThuaDatModel: thuaDatModel,
                    TaiSan: taiSanModel
                ));
            }

            return results;
        }
    }
}
