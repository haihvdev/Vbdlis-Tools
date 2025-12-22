using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    public HomeViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
    }

    public string LoggedInUsername => _mainWindowViewModel.LoggedInUsername;

    public string LoggedInServer => _mainWindowViewModel.LoggedInServer;

    public ICommand LogoutCommand => _mainWindowViewModel.LogoutCommand;
}
