using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.Services.Vbdlis;
using Haihv.Vbdlis.Tools.Desktop.Views;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels;

public partial class CungCapThongTinViewModel : ViewModelBase
{
    private readonly CungCapThongTinGiayChungNhanService _searchService;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _searchProgress = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMaximum = 100;

    [ObservableProperty]
    private SearchResultModel? _selectedResult;

    [ObservableProperty]
    private GiayChungNhanItem? _selectedItem;

    [ObservableProperty]
    private bool _isDetailVisible;

    public ObservableCollection<SearchResultModel> SearchResults { get; } = new();

    public CungCapThongTinViewModel(CungCapThongTinGiayChungNhanService searchService)
    {
        _searchService = searchService;
    }

    partial void OnSelectedResultChanged(SearchResultModel? value)
    {
        if (value?.Response != null && value.Response.Data?.Count > 0)
        {
            SelectedItem = value.Response.Data[0];
            IsDetailVisible = true;
        }
        else
        {
            SelectedItem = null;
            IsDetailVisible = false;
        }
    }

    [RelayCommand]
    private async Task SearchBySoGiayToAsync()
    {
        Log.Information("SearchBySoGiayToAsync started");

        var mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            Log.Warning("MainWindow is null, cannot show dialog");
            return;
        }

        Log.Information("Showing search input dialog");
        var dialog = new SearchInputWindow("Tìm kiếm theo Số Giấy Tờ", "Nhập số giấy tờ (mỗi dòng hoặc phân cách bằng ;):");
        var result = await dialog.ShowDialog(mainWindow);

        Log.Information("Dialog closed. IsConfirmed: {IsConfirmed}, Input: {Input}", result.IsConfirmed, result.Input);

        if (result.IsConfirmed && !string.IsNullOrWhiteSpace(result.Input))
        {
            var items = ParseInput(result.Input);
            Log.Information("Parsed {Count} items: {Items}", items.Length, string.Join(", ", items));

            await PerformSearchAsync(items, async (item) =>
            {
                return await _searchService.SearchAsync(soGiayTo: item);
            }, "số giấy tờ");
        }
        else
        {
            Log.Information("Search cancelled or no input");
        }
    }

    [RelayCommand]
    private async Task SearchBySoPhatHanhAsync()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        var dialog = new SearchInputWindow("Tìm kiếm theo Số Phát Hành", "Nhập số phát hành (mỗi dòng hoặc phân cách bằng ;):");
        var result = await dialog.ShowDialog(mainWindow);

        if (result.IsConfirmed && !string.IsNullOrWhiteSpace(result.Input))
        {
            var items = ParseInput(result.Input);
            await PerformSearchAsync(items, async (item) =>
            {
                return await _searchService.SearchAsync(soPhatHanh: item);
            }, "số phát hành");
        }
    }

    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    private string[] ParseInput(string input)
    {
        return input
            .Split(new[] { '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    private async Task PerformSearchAsync(
        string[] items,
        Func<string, Task<AdvancedSearchGiayChungNhanResponse?>> searchFunc,
        string searchType)
    {
        if (items.Length == 0) return;

        IsSearching = true;
        ProgressMaximum = items.Length;
        ProgressValue = 0;

        // Clear previous results
        SearchResults.Clear();

        try
        {
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                SearchProgress = $"Đang tìm {searchType}: {item} ({i + 1}/{items.Length})";

                try
                {
                    var response = await searchFunc(item);

                    if (response != null)
                    {
                        // Add to results
                        SearchResults.Add(new SearchResultModel
                        {
                            SearchQuery = item,
                            Response = response,
                            SearchType = searchType,
                            SearchTime = DateTime.Now
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Console.WriteLine($"Error searching {item}: {ex.Message}");
                }

                ProgressValue = i + 1;
            }

            SearchProgress = $"Hoàn thành! Tìm được {SearchResults.Count} kết quả.";
        }
        finally
        {
            IsSearching = false;
        }
    }
}
