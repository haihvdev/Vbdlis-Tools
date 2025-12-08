using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haihv.Tools.Hsq.Helpers;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.Services.Vbdlis;
using Haihv.Vbdlis.Tools.Desktop.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels;

public partial class CungCapThongTinViewModel(CungCapThongTinGiayChungNhanService searchService) : ViewModelBase
{
    private readonly CungCapThongTinGiayChungNhanService _searchService = searchService;
    private Action<AdvancedSearchGiayChungNhanResponse>? _updateDataGridAction;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _searchProgress = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMaximum = 100;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private SearchResultModel? _selectedResult;

    [ObservableProperty]
    private GiayChungNhanItem? _currentItem;

    [ObservableProperty]
    private bool _isDetailVisible;

    public ObservableCollection<SearchResultModel> SearchResults { get; } = new();

    /// <summary>
    /// Đăng ký action để cập nhật DataGrid từ View
    /// </summary>
    public void RegisterDataGridUpdater(Action<AdvancedSearchGiayChungNhanResponse> updateAction)
    {
        _updateDataGridAction = updateAction;
    }

    partial void OnSelectedResultChanged(SearchResultModel? value)
    {
        if (value?.Response != null && value.Response.Data?.Count > 0)
        {
            CurrentItem = value.Response.Data[0];
            IsDetailVisible = true;
        }
        else
        {
            CurrentItem = null;
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

            try
            {
                Log.Information("Starting PerformSearchAsync...");
                await PerformSearchAsync(items, async (item) =>
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        Log.Information("Empty item, skipping search");
                        return null;
                    }
                    var modifiedItem = item.NormalizePersonalId();
                    if (modifiedItem == null)
                    {
                        Log.Information("Item normalization returned null for item: {Item}", item);
                        return null;
                    }
                    Log.Information("Searching for item: {Item}", modifiedItem);
                    var result = await _searchService.SearchAsync(soGiayTo: modifiedItem);
                    if (result != null)
                    {
                        Log.Information("SearchAsync returned {Count} results for item: {Item}", result.Data?.Count ?? 0, item);
                        return result;
                    }
                    else
                    {
                        Log.Information("Thử lại sau khi bỏ số 0 ở đầu cho item: {Item}", item);
                        modifiedItem = modifiedItem.TrimStart('0');
                        if (item.Length == modifiedItem.Length)
                        {
                            Log.Information("No leading zeros to remove for item: {Item}", item);
                            return null;
                        }
                        result = await _searchService.SearchAsync(soGiayTo: modifiedItem);
                        if (result != null)
                        {
                            Log.Information("SearchAsync returned {Count} results for modified item: {Item}", result.Data?.Count ?? 0, item);
                            return result;
                        }
                        else
                        {
                            Log.Information("Thử lại sau khi bỏ 0 ở đầu cho item lần 2: {Item}", item);
                            modifiedItem = modifiedItem.TrimStart('0');
                            if (item.Length == modifiedItem.Length)
                            {
                                Log.Information("No leading zeros to remove for item: {Item}", item);
                                return null;
                            }
                            return await _searchService.SearchAsync(soGiayTo: modifiedItem);
                        }

                    }

                }, "số giấy tờ");
                Log.Information("PerformSearchAsync completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in PerformSearchAsync");
                SearchProgress = $"Lỗi: {ex.Message}";
                IsSearching = false;
            }
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
        var (IsConfirmed, Input) = await dialog.ShowDialog(mainWindow);

        if (IsConfirmed && !string.IsNullOrWhiteSpace(Input))
        {
            var items = ParseInput(Input);
            await PerformSearchAsync(items, async (item) =>
            {
                var modifiedItem = item.NormalizedSoPhatHanh();
                return await _searchService.SearchAsync(soPhatHanh: modifiedItem);
            }, "số phát hành");
        }
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    private string[] ParseInput(string input)
    {
        return [.. input
            .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))];
    }

    private async Task PerformSearchAsync(
        string[] items,
        Func<string, Task<AdvancedSearchGiayChungNhanResponse?>> searchFunc,
        string searchType)
    {
        Log.Information("PerformSearchAsync called with {Count} items", items.Length);
        if (items.Length == 0) return;

        IsSearching = true;
        ProgressMaximum = items.Length;
        ProgressValue = 0;
        ProgressPercentage = 0;

        // Clear previous results
        SearchResults.Clear();

        // Clear DataGrid trước khi bắt đầu tìm kiếm
        ClearDataGridResults();

        Log.Information("Starting search loop...");
        Log.Information("Ensuring CungCapThongTin page...");
        await _searchService.EnsureCungCapThongTinPageAsync();

        // Tổng hợp tất cả kết quả vào một response duy nhất
        var allData = new List<GiayChungNhanItem>();
        int totalFound = 0;

        try
        {
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                SearchProgress = $"Đang tìm {searchType}: {item}";
                Log.Information("Calling search service for item: {Item}", item);

                try
                {
                    var response = await searchFunc(item);
                    Log.Information("Search service returned for item: {Item}, Response is null: {IsNull}", item, response == null);

                    if (response != null)
                    {
                        var searchResult = new SearchResultModel
                        {
                            SearchQuery = item,
                            Response = response,
                            SearchType = searchType,
                            SearchTime = DateTime.Now
                        };

                        // Add to results
                        SearchResults.Add(searchResult);

                        // Tổng hợp tất cả data[] vào danh sách chung
                        if (response.Data?.Count > 0)
                        {
                            allData.AddRange(response.Data);
                            totalFound += response.Data.Count;
                            SearchProgress = $"Tìm thấy: {item} - {response.Data.Count} kết quả";

                            // Update DataGrid ngay lập tức với kết quả hiện tại
                            UpdateDataGridResults(allData);
                        }
                        else
                        {
                            SearchProgress = $"Không tìm thấy: {item}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Log.Error(ex, "Error searching {Item}", item);
                    SearchProgress = $"Lỗi khi tìm: {item}";
                }

                ProgressValue = i + 1;
                ProgressPercentage = (ProgressValue / ProgressMaximum) * 100;

                // Small delay to show progress
                if (i < items.Length - 1)
                {
                    await Task.Delay(500);
                }
            }

            SearchProgress = $"Hoàn thành! Đã tìm {items.Length} mục, tìm thấy {totalFound} kết quả.";

            // Cập nhật DataGrid lần cuối với tất cả kết quả (nếu chưa có kết quả nào)
            if (totalFound > 0)
            {
                UpdateDataGridResults(allData);
            }
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Xóa kết quả trong DataGrid control
    /// </summary>
    private void ClearDataGridResults()
    {
        Log.Information("Clearing DataGrid results");
        var emptyResponse = new AdvancedSearchGiayChungNhanResponse
        {
            Data = new List<GiayChungNhanItem>(),
            RecordsTotal = 0,
            RecordsFiltered = 0
        };
        _updateDataGridAction?.Invoke(emptyResponse);
    }

    /// <summary>
    /// Cập nhật kết quả vào DataGrid control
    /// </summary>
    private void UpdateDataGridResults(List<GiayChungNhanItem> allData)
    {
        if (allData.Count == 0)
        {
            Log.Information("No results to display in DataGrid");
            return;
        }

        // Tạo response tổng hợp
        var combinedResponse = new AdvancedSearchGiayChungNhanResponse
        {
            Data = allData,
            RecordsTotal = allData.Count,
            RecordsFiltered = allData.Count
        };

        Log.Information("Updating DataGrid with {Count} items", allData.Count);

        // Gọi action để cập nhật DataGrid từ View
        _updateDataGridAction?.Invoke(combinedResponse);
    }
}
