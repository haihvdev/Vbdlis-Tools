using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Views.Controls;

public partial class SearchInputControl : UserControl
{
    private readonly ILogger _logger = Log.ForContext<SearchInputControl>();
    public SearchInputControl()
    {
        InitializeComponent();
    }

    private void OnSearchHistoryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not CungCapThongTinViewModel viewModel)
        {
            return;
        }

        if (sender is not ListBox { SelectedItem: SearchHistoryEntry entry }) return;
        viewModel.SearchFromHistoryCommand.Execute(entry);
        e.Handled = true;
    }

    private async void OnHistoryTitleLostFocus(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not CungCapThongTinViewModel viewModel)
            {
                return;
            }

            if (sender is TextBox textBox && textBox.Tag is SearchHistoryEntry entry)
            {
                await viewModel.SaveHistoryTitleCommand.ExecuteAsync(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save history title");
        }
    }

    private async void OnHistoryTitleKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (DataContext is not CungCapThongTinViewModel viewModel)
            {
                return;
            }

            if (sender is not TextBox { Tag: SearchHistoryEntry entry }) return;
            if (e.Key == Key.Enter)
            {
                await viewModel.SaveHistoryTitleCommand.ExecuteAsync(entry);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                viewModel.CancelEditHistoryCommand.Execute(entry);
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save history title");
        }
    }
}