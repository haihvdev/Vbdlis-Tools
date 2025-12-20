using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels;

/// <summary>
/// ViewModel cho UserControl chọn các cấp DVHC (Tỉnh/Huyện/Xã)
/// </summary>
public partial class DvhcSelectorViewModel(DonViHanhChinhViewModel dvhcViewModel, bool isHorizontalLayout = false) : ObservableObject
{
    private readonly ILogger _logger = Log.ForContext<DvhcSelectorViewModel>();
    private readonly DonViHanhChinhViewModel _dvhcViewModel = dvhcViewModel;

    [ObservableProperty]
    private ObservableCollection<DvhcCapHuyen> _huyenList = [];

    [ObservableProperty]
    private ObservableCollection<DvhcCapXa> _xaList = [];

    [ObservableProperty]
    private DvhcCapHuyen? _selectedHuyen;

    [ObservableProperty]
    private DvhcCapXa? _selectedXa;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Layout hiển thị: true = ngang (3 cấp 1 hàng), false = dọc (mỗi cấp 1 hàng)
    /// Mặc định là dọc
    /// </summary>
    public bool IsHorizontalLayout { get; set; } = isHorizontalLayout;

    /// <summary>
    /// Khởi tạo dữ liệu DVHC
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Đang tải dữ liệu DVHC...";

            // Đảm bảo page đã được khởi tạo
            await _dvhcViewModel.EnsurePageAsync();

            // Load danh sách huyện
            await LoadHuyenListAsync(forceReload: false);

            StatusMessage = "Đã tải dữ liệu DVHC thành công";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lỗi khởi tạo dữ liệu DVHC");
            StatusMessage = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Load danh sách huyện
    /// </summary>
    private async Task LoadHuyenListAsync(bool forceReload = false)
    {
        try
        {
            var huyenData = await _dvhcViewModel.GetCapHuyenAsync(forceReload);

            HuyenList.Clear();
            foreach (var huyen in huyenData)
            {
                HuyenList.Add(huyen);
            }

            _logger.Debug("Đã tải {Count} huyện", HuyenList.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lỗi load danh sách huyện");
            throw;
        }
    }

    /// <summary>
    /// Xử lý khi chọn huyện
    /// </summary>
    partial void OnSelectedHuyenChanged(DvhcCapHuyen? value)
    {
        if (value == null)
        {
            XaList.Clear();
            SelectedXa = null;
            return;
        }

        _ = LoadXaListAsync(value.Id);
    }

    /// <summary>
    /// Load danh sách xã theo huyện đã chọn
    /// </summary>
    private async Task LoadXaListAsync(int huyenId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Đang tải danh sách xã...";

            // Chọn huyện trên trang web để trigger update dropdown xã
            await _dvhcViewModel.SelectCapHuyenAsync(huyenId);

            // Lấy danh sách xã
            var xaData = await _dvhcViewModel.GetCapXaAsync(forceReload: false);

            XaList.Clear();
            foreach (var xa in xaData)
            {
                XaList.Add(xa);
            }

            _logger.Debug("Đã tải {Count} xã cho huyện {HuyenId}", XaList.Count, huyenId);
            StatusMessage = $"Đã tải {XaList.Count} xã/phường";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lỗi load danh sách xã cho huyện {HuyenId}", huyenId);
            StatusMessage = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Làm mới dữ liệu DVHC từ web
    /// </summary>
    [RelayCommand]
    private async Task RefreshDvhcDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Đang làm mới dữ liệu DVHC...";

            // Load lại danh sách huyện từ web
            await LoadHuyenListAsync(forceReload: true);

            // Reset các selection
            SelectedHuyen = null;
            SelectedXa = null;
            XaList.Clear();

            StatusMessage = "Đã làm mới dữ liệu DVHC thành công";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lỗi làm mới dữ liệu DVHC");
            StatusMessage = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Lấy thông tin DVHC đã chọn dưới dạng text
    /// </summary>
    public string GetSelectedDvhcText()
    {
        if (SelectedXa != null)
        {
            return $"{SelectedXa.Name}, {SelectedHuyen?.Name}";
        }

        if (SelectedHuyen != null)
        {
            return SelectedHuyen.Name;
        }

        return string.Empty;
    }
}
