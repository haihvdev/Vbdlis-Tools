using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haihv.Vbdlis.Tools.Desktop.Extensions;
using Haihv.Vbdlis.Tools.Desktop.Models;
using OfficeOpenXml;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels;

public partial class KetQuaTimKiemDataGridViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<KetQuaTimKiemModel> _ketQuaTimKiemList = [];

    [ObservableProperty]
    private KetQuaTimKiemModel? _selectedItem;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isExporting;

    private bool _isUpdatingCollection;
    private List<KetQuaTimKiemModel>? _pendingModels;
    private string? _pendingStatusMessage;

    public KetQuaTimKiemDataGridViewModel()
    {
        // Set EPPlus license context
        ExcelPackage.License.SetNonCommercialPersonal("Hoàng Việt Hải");
    }

    /// <summary>
    /// Cập nhật danh sách kết quả tìm kiếm
    /// </summary>
    public void UpdateData(AdvancedSearchGiayChungNhanResponse? response)
    {
        if (response?.Data == null || response.Data.Count == 0)
        {
            UpdateCollectionOnUiThread([], "Không có dữ liệu");
            return;
        }

        var models = response.ToKetQuaTimKiemModels();
        UpdateCollectionOnUiThread(models, $"Tìm thấy {models.Count} kết quả");
    }

    private void UpdateCollectionOnUiThread(IReadOnlyList<KetQuaTimKiemModel> models, string statusMessage)
    {
        void ApplyUpdates()
        {
            if (_isUpdatingCollection)
            {
                _pendingModels = models.ToList();
                _pendingStatusMessage = statusMessage;
                return;
            }

            _isUpdatingCollection = true;

            try
            {
                // Replace the entire collection to avoid triggering multiple change notifications
                var newList = new ObservableCollection<KetQuaTimKiemModel>(models);
                KetQuaTimKiemList = newList;
                SelectedItem = null;
                StatusMessage = statusMessage;
            }
            finally
            {
                _isUpdatingCollection = false;

                if (_pendingModels != null)
                {
                    var pendingModels = _pendingModels;
                    var pendingStatus = _pendingStatusMessage ?? statusMessage;
                    _pendingModels = null;
                    _pendingStatusMessage = null;
                    UpdateCollectionOnUiThread(pendingModels, pendingStatus);
                }
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyUpdates();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyUpdates);
        }
    }

    /// <summary>
    /// Xuất Excel dạng Compact (KetQuaTimKiemModel)
    /// </summary>
    [RelayCommand]
    private async Task ExportExcelCompactAsync()
    {
        if (KetQuaTimKiemList.Count == 0)
        {
            StatusMessage = "Không có dữ liệu để xuất";
            return;
        }

        try
        {
            IsExporting = true;
            StatusMessage = "Đang xuất Excel...";

            var fileName = $"KetQuaTimKiem_Compact_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

            await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Kết quả tìm kiếm");

                // Header - 5 columns: STT + 4 grid columns
                worksheet.Cells[1, 1].Value = "STT";
                worksheet.Cells[1, 2].Value = "Thông tin chủ sử dụng";
                worksheet.Cells[1, 3].Value = "Thông tin giấy chứng nhận";
                worksheet.Cells[1, 4].Value = "Thông tin thửa đất";
                worksheet.Cells[1, 5].Value = "Thông tin tài sản";

                // Format header
                using (var range = worksheet.Cells[1, 1, 1, 5])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }

                // Data
                for (int i = 0; i < KetQuaTimKiemList.Count; i++)
                {
                    var item = KetQuaTimKiemList[i];
                    var row = i + 2;

                    // STT
                    worksheet.Cells[row, 1].Value = i + 1;

                    // Thông tin chủ sử dụng
                    worksheet.Cells[row, 2].Value = item.ChuSuDungCompact;

                    // Thông tin giấy chứng nhận
                    var gcnInfo = $"Số phát hành: {item.GiayChungNhanModel.SoPhatHanh}";
                    if (!string.IsNullOrEmpty(item.GiayChungNhanModel.SoVaoSo))
                    {
                        gcnInfo += $"\nSố vào sổ: {item.GiayChungNhanModel.SoVaoSo}";
                    }
                    if (item.GiayChungNhanModel.NgayVaoSo.HasValue && item.GiayChungNhanModel.NgayVaoSo.Value >= new DateTime(1900, 1, 1))
                    {
                        gcnInfo += $"\nNgày vào sổ: {item.GiayChungNhanModel.NgayVaoSo.Value:dd/MM/yyyy}";
                    }
                    worksheet.Cells[row, 3].Value = gcnInfo;

                    // Thông tin thửa đất - sử dụng ThuaDatCompact
                    worksheet.Cells[row, 4].Value = item.ThuaDatCompact;

                    // Thông tin tài sản - sử dụng TaiSanCompact
                    worksheet.Cells[row, 5].Value = item.TaiSanCompact;

                    // Enable text wrapping for multi-line content
                    worksheet.Cells[row, 2, row, 5].Style.WrapText = true;
                    worksheet.Cells[row, 2, row, 5].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                }

                // Set column widths
                worksheet.Column(1).Width = 8;  // STT
                worksheet.Column(2).Width = 35; // Chủ sử dụng
                worksheet.Column(3).Width = 30; // Giấy chứng nhận
                worksheet.Column(4).Width = 40; // Thửa đất
                worksheet.Column(5).Width = 35; // Tài sản

                // Save
                var fileInfo = new FileInfo(filePath);
                package.SaveAs(fileInfo);
            });

            StatusMessage = $"Đã xuất file: {fileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi xuất Excel: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// Xuất Excel đầy đủ (tất cả thuộc tính)
    /// </summary>
    [RelayCommand]
    private async Task ExportExcelFullAsync()
    {
        if (KetQuaTimKiemList.Count == 0)
        {
            StatusMessage = "Không có dữ liệu để xuất";
            return;
        }

        try
        {
            static string JoinLines(IEnumerable<string?>? values)
            {
                if (values == null)
                    return "";

                var parts = values
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!.Trim());

                return string.Join(Environment.NewLine, parts);
            }

            IsExporting = true;
            StatusMessage = "Đang xuất Excel đầy đủ...";

            var fileName = $"KetQuaTimKiem_Full_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

            await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Kết quả tìm kiếm");

                // Header - Chủ sử dụng
                worksheet.Cells[1, 1].Value = "STT";
                worksheet.Cells[1, 2].Value = "Tên chủ sử dụng";
                worksheet.Cells[1, 3].Value = "Số giấy tờ";
                worksheet.Cells[1, 4].Value = "Địa chỉ chủ sử dụng";

                // Header - Giấy chứng nhận
                worksheet.Cells[1, 5].Value = "Số phát hành";
                worksheet.Cells[1, 6].Value = "Số vào sổ";
                worksheet.Cells[1, 7].Value = "Ngày vào sổ";

                // Header - Thửa đất
                worksheet.Cells[1, 8].Value = "Số tờ bản đồ";
                worksheet.Cells[1, 9].Value = "Số thửa đất";
                worksheet.Cells[1, 10].Value = "Diện tích";
                worksheet.Cells[1, 11].Value = "Mục đích sử dụng";
                worksheet.Cells[1, 12].Value = "Địa chỉ thửa đất";

                // Header - Tài sản
                worksheet.Cells[1, 13].Value = "Loại tài sản";
                worksheet.Cells[1, 14].Value = "Diện tích xây dựng";
                worksheet.Cells[1, 15].Value = "Diện tích sử dụng";
                worksheet.Cells[1, 16].Value = "Số tầng";
                worksheet.Cells[1, 17].Value = "Địa chỉ tài sản";

                // Format header
                using (var range = worksheet.Cells[1, 1, 1, 17])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    range.Style.WrapText = true;
                }

                // Data
                for (int i = 0; i < KetQuaTimKiemList.Count; i++)
                {
                    var item = KetQuaTimKiemList[i];
                    var row = i + 2;

                    worksheet.Cells[row, 1].Value = i + 1;
                    worksheet.Cells[row, 2].Value = JoinLines(item.ListChuSuDung?.Select(c => c.TenChu(false).Trim()));
                    worksheet.Cells[row, 3].Value = JoinLines(item.ListChuSuDung?.Select(c => c.SoGiayTo));
                    worksheet.Cells[row, 4].Value = JoinLines(item.ListChuSuDung?.Select(c => c.DiaChi));
                    // Giấy chứng nhận
                    worksheet.Cells[row, 5].Value = item.GiayChungNhanModel.SoPhatHanh;
                    worksheet.Cells[row, 6].Value = item.GiayChungNhanModel.SoVaoSo;
                    worksheet.Cells[row, 7].Value = item.GiayChungNhanModel.NgayVaoSo.HasValue && item.GiayChungNhanModel.NgayVaoSo.Value >= new DateTime(1900, 1, 1)
                        ? item.GiayChungNhanModel.NgayVaoSo.Value.ToString("dd/MM/yyyy")
                        : "";

                    // Thửa đất - lấy tất cả và nối bằng xuống dòng
                    worksheet.Cells[row, 8].Value = JoinLines(item.ListThuaDat?.Select(td => td.SoToBanDo));
                    worksheet.Cells[row, 9].Value = JoinLines(item.ListThuaDat?.Select(td => td.SoThuaDat));
                    worksheet.Cells[row, 10].Value = JoinLines(item.ListThuaDat?.Select(td => td.HasDienTich ? td.DienTich!.Value.ToString() : ""));
                    worksheet.Cells[row, 11].Value = JoinLines(item.ListThuaDat?.Select(td => td.HasMucDichSuDung ? td.MucDichSuDungFormatted : td.MucDichSuDung));
                    worksheet.Cells[row, 12].Value = JoinLines(item.ListThuaDat?.Select(td => td.DiaChi));

                    // Tài sản - lấy tất cả và nối bằng xuống dòng
                    worksheet.Cells[row, 13].Value = JoinLines(item.ListTaiSan?.Select(ts => ts.TenTaiSan));
                    worksheet.Cells[row, 14].Value = JoinLines(item.ListTaiSan?.Select(ts => ts.DienTichXayDung > 0 ? ts.DienTichXayDung!.Value.ToString() : ""));
                    worksheet.Cells[row, 15].Value = JoinLines(item.ListTaiSan?.Select(ts => ts.DienTichSuDung > 0 ? ts.DienTichSuDung!.Value.ToString() : ""));
                    worksheet.Cells[row, 16].Value = JoinLines(item.ListTaiSan?.Select(ts => ts.SoTang));
                    worksheet.Cells[row, 17].Value = JoinLines(item.ListTaiSan?.Select(ts => ts.DiaChi));

                    // Enable text wrapping for multi-line content
                    worksheet.Cells[row, 2, row, 17].Style.WrapText = true;
                    worksheet.Cells[row, 2, row, 17].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                }

                // Auto fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Save
                var fileInfo = new FileInfo(filePath);
                package.SaveAs(fileInfo);
            });

            StatusMessage = $"Đã xuất file: {fileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi xuất Excel: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }
}
