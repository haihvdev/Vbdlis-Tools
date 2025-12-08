using Avalonia.Controls;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;

namespace Haihv.Vbdlis.Tools.Desktop.Views;

public partial class CungCapThongTinView : UserControl
{
    public CungCapThongTinView()
    {
        InitializeComponent();

        // Đăng ký khi DataContext thay đổi
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // Khi DataContext được set (từ XAML binding), đăng ký updater
        if (DataContext is CungCapThongTinViewModel viewModel)
        {
            viewModel.RegisterDataGridUpdater(UpdateDataGrid);
        }
    }

    public void SetViewModel(CungCapThongTinViewModel viewModel)
    {
        DataContext = viewModel;

        // Đăng ký action để cập nhật DataGrid khi ViewModel có kết quả
        viewModel.RegisterDataGridUpdater(UpdateDataGrid);
    }

    /// <summary>
    /// Cập nhật DataGrid với kết quả tìm kiếm
    /// </summary>
    private void UpdateDataGrid(AdvancedSearchGiayChungNhanResponse response)
    {
        // Tìm control ResultsDataGrid trong XAML
        var dataGridControl = this.FindControl<KetQuaTimKiemDataGridControl>("ResultsDataGrid");

        if (dataGridControl != null)
        {
            // Cập nhật dữ liệu vào DataGrid
            dataGridControl.UpdateData(response);
        }
    }
}
