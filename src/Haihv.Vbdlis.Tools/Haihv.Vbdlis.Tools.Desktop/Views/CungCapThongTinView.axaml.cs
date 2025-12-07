using Avalonia.Controls;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;

namespace Haihv.Vbdlis.Tools.Desktop.Views;

public partial class CungCapThongTinView : UserControl
{
    public CungCapThongTinView()
    {
        InitializeComponent();
    }

    public void SetViewModel(CungCapThongTinViewModel viewModel)
    {
        DataContext = viewModel;
    }
}
