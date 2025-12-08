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
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Cập nhật danh sách kết quả tìm kiếm
    /// </summary>
    public void UpdateData(AdvancedSearchGiayChungNhanResponse? response)
    {
        if (response?.Data == null || response.Data.Count == 0)
        {
            UpdateCollectionOnUiThread(Array.Empty<KetQuaTimKiemModel>(), "Không có dữ liệu");
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

                // Header
                worksheet.Cells[1, 1].Value = "STT";
                worksheet.Cells[1, 2].Value = "Chủ sử dụng";
                worksheet.Cells[1, 3].Value = "Số phát hành";
                worksheet.Cells[1, 4].Value = "Số vào sổ";
                worksheet.Cells[1, 5].Value = "Ngày vào sổ";
                worksheet.Cells[1, 6].Value = "Số tờ bản đồ";
                worksheet.Cells[1, 7].Value = "Số thửa đất";
                worksheet.Cells[1, 8].Value = "Địa chỉ tài sản";

                // Format header
                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Data
                for (int i = 0; i < KetQuaTimKiemList.Count; i++)
                {
                    var item = KetQuaTimKiemList[i];
                    var row = i + 2;

                    worksheet.Cells[row, 1].Value = i + 1;
                    worksheet.Cells[row, 2].Value = item.ChuSuDung.DanhSachChuSoHuu;
                    worksheet.Cells[row, 3].Value = item.GiayChungNhanModel.SoPhatHanh;
                    worksheet.Cells[row, 4].Value = item.GiayChungNhanModel.SoVaoSo;
                    worksheet.Cells[row, 5].Value = item.GiayChungNhanModel.NgayVaoSo.HasValue && item.GiayChungNhanModel.NgayVaoSo.Value >= new DateTime(1900, 1, 1)
                        ? item.GiayChungNhanModel.NgayVaoSo.Value.ToString("dd/MM/yyyy")
                        : "";
                    worksheet.Cells[row, 6].Value = item.ThuaDatModel.SoToBanDo;
                    worksheet.Cells[row, 7].Value = item.ThuaDatModel.SoThuaDat;
                    worksheet.Cells[row, 8].Value = item.ThuaDatModel.DiaChi;
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
                worksheet.Cells[1, 2].Value = "Chủ sử dụng";

                // Header - Giấy chứng nhận
                worksheet.Cells[1, 3].Value = "Số phát hành";
                worksheet.Cells[1, 4].Value = "Số vào sổ";
                worksheet.Cells[1, 5].Value = "Ngày vào sổ";

                // Header - Thửa đất
                worksheet.Cells[1, 6].Value = "Số tờ bản đồ";
                worksheet.Cells[1, 7].Value = "Số thửa đất";
                worksheet.Cells[1, 8].Value = "Diện tích";
                worksheet.Cells[1, 9].Value = "Mục đích sử dụng";
                worksheet.Cells[1, 10].Value = "Địa chỉ thửa đất";
                // Header - Tài sản
                worksheet.Cells[1, 11].Value = "Loại tài sản";
                worksheet.Cells[1, 12].Value = "Diện tích xây dựng";
                worksheet.Cells[1, 13].Value = "Diện tích sử dụng";
                worksheet.Cells[1, 14].Value = "Số tầng";
                worksheet.Cells[1, 15].Value = "Địa chỉ tài sản";

                // Format header
                using (var range = worksheet.Cells[1, 1, 1, 15])
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
                    worksheet.Cells[row, 2].Value = item.ChuSuDung.DanhSachChuSoHuu;
                    // Giấy chứng nhận
                    worksheet.Cells[row, 3].Value = item.GiayChungNhanModel.SoPhatHanh;
                    worksheet.Cells[row, 4].Value = item.GiayChungNhanModel.SoVaoSo;
                    worksheet.Cells[row, 5].Value = item.GiayChungNhanModel.NgayVaoSo.HasValue && item.GiayChungNhanModel.NgayVaoSo.Value >= new DateTime(1900, 1, 1)
                        ? item.GiayChungNhanModel.NgayVaoSo.Value.ToString("dd/MM/yyyy")
                        : "";

                    // Thửa đất
                    worksheet.Cells[row, 6].Value = item.ThuaDatModel.SoToBanDo;
                    worksheet.Cells[row, 7].Value = item.ThuaDatModel.SoThuaDat;
                    worksheet.Cells[row, 8].Value = item.ThuaDatModel.DienTich > 0 ? item.ThuaDatModel.DienTich : (object)"";
                    worksheet.Cells[row, 9].Value = item.ThuaDatModel.MucDichSuDung;
                    worksheet.Cells[row, 10].Value = item.ThuaDatModel.DiaChi;
                    // Tài sản
                    worksheet.Cells[row, 11].Value = item.TaiSan.LoaiTaiSan;
                    worksheet.Cells[row, 12].Value = item.TaiSan.DienTichXayDung > 0 ? item.TaiSan.DienTichXayDung : (object)"";
                    worksheet.Cells[row, 13].Value = item.TaiSan.DienTichSuDung > 0 ? item.TaiSan.DienTichSuDung : (object)"";
                    worksheet.Cells[row, 14].Value = item.TaiSan.SoTang;
                    worksheet.Cells[row, 15].Value = item.TaiSan.DiaChi;
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
