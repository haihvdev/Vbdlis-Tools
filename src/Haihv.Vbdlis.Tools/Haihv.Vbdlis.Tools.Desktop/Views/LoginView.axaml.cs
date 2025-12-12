using Avalonia.Controls;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;

namespace Haihv.Vbdlis.Tools.Desktop;

public partial class LoginControl : UserControl
{
    public LoginControl()
    {
        InitializeComponent();

        // Initialize ViewModel
        DataContext ??= new LoginViewModel();
        InitializeLoginViewModel();
    }

    public LoginControl(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        InitializeLoginViewModel();
    }

    private void InitializeLoginViewModel()
    {
        if (ViewModel == null)
            return;
        if (string.IsNullOrWhiteSpace(ViewModel.Server))
            ViewModel.Server = "https://bgi.mplis.gov.vn/dc/";
    }

    /// <summary>
    /// Gets the LoginViewModel instance
    /// </summary>
    public LoginViewModel? ViewModel => DataContext as LoginViewModel;
}