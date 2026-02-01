using System;
using Avalonia.Controls;
using Haihv.Vbdlis.Tools.Desktop.Models.Vbdlis;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;

namespace Haihv.Vbdlis.Tools.Desktop.Views.Controls;

public partial class KetQuaTimKiemDataGridControl : UserControl
{
    public KetQuaTimKiemDataGridControl()
    {
        InitializeComponent();
        DataContext ??= new KetQuaTimKiemDataGridViewModel();
    }

    public KetQuaTimKiemDataGridControl(KetQuaTimKiemDataGridViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// Gets the KetQuaTimKiemDataGridViewModel instance
    /// </summary>
    public KetQuaTimKiemDataGridViewModel? ViewModel => DataContext as KetQuaTimKiemDataGridViewModel;

    /// <summary>
    /// Cập nhật dữ liệu từ response
    /// </summary>
    public void UpdateData(AdvancedSearchGiayChungNhanResponse? response, DateTime? oldestCacheTime)
    {
        ViewModel?.UpdateData(response, oldestCacheTime);
    }

    /// <summary>
    /// Event handler cho MenuItem Xuất Excel Compact
    /// </summary>
    private async void OnExportExcelCompactClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (ViewModel?.ExportExcelCompactCommand.CanExecute(topLevel) == true)
        {
            await ViewModel.ExportExcelCompactCommand.ExecuteAsync(topLevel);
        }
    }

    /// <summary>
    /// Event handler cho MenuItem Xuất Excel Full
    /// </summary>
    private async void OnExportExcelFullClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (ViewModel?.ExportExcelFullCommand.CanExecute(topLevel) == true)
        {
            await ViewModel.ExportExcelFullCommand.ExecuteAsync(topLevel);
        }
    }
}