using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Views;

public partial class SearchInputWindow : Window
{
    public string Prompt { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public bool IsConfirmed { get; private set; }

    public SearchInputWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public SearchInputWindow(string title, string prompt) : this()
    {
        Title = title;  // Use base Window.Title property directly
        Prompt = prompt;
    }

    private void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }

    public new async Task<(bool IsConfirmed, string Input)> ShowDialog(Window owner)
    {
        await ShowDialog<object?>(owner);
        return (IsConfirmed, Input);
    }
}
