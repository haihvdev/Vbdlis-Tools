using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haihv.Tools.Hsq.Helpers;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.Models.Vbdlis;
using Haihv.Vbdlis.Tools.Desktop.Services.Data;
using Haihv.Vbdlis.Tools.Desktop.Services.Vbdlis;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels;

public partial class CungCapThongTinViewModel : ViewModelBase
{
    private const string SearchTypeSoGiayToKey = "so-giay-to";
    private const string SearchTypeSoPhatHanhKey = "so-phat-hanh";
    private const string SearchTypeThuaDatKey = "thua-dat";

    private readonly CungCapThongTinGiayChungNhanService _searchService;
    private readonly SearchHistoryService _searchHistoryService;
    private readonly SearchCacheService _searchCacheService;
    private Action<AdvancedSearchGiayChungNhanResponse>? _updateDataGridAction;
    private Action<AdvancedSearchGiayChungNhanResponse, DateTime?>? _updateDataGridActionWithCacheTime;

    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private string _searchProgress = string.Empty;

    [ObservableProperty] private bool _isInitializing;

    [ObservableProperty] private double _progressValue;

    [ObservableProperty] private double _progressMaximum = 100;

    [ObservableProperty] private double _progressPercentage;

    [ObservableProperty] private SearchResultModel? _selectedResult;

    [ObservableProperty] private GiayChungNhanItem? _currentItem;

    [ObservableProperty] private bool _isDetailVisible;

    [ObservableProperty] private int _completedItems;

    [ObservableProperty] private int _totalItems;

    [ObservableProperty] private int _foundItems;

    [ObservableProperty] private string _currentSearchItem = string.Empty;

    [ObservableProperty] private string _currentSearchType = string.Empty;

    [ObservableProperty] private string _searchInput = string.Empty;

    [ObservableProperty] private ObservableCollection<SearchHistoryEntry> _searchHistory = [];

    [ObservableProperty] private SearchHistoryEntry? _selectedSearchHistory;

    [ObservableProperty] private int _selectedSearchTabIndex;

    [ObservableProperty] private ObservableCollection<CacheRefreshOption> _refreshOptions = [];

    [ObservableProperty] private CacheRefreshOption? _selectedRefreshOption;

    public bool IsSoGiayToMode => SelectedSearchTabIndex == 0;

    public bool IsSoPhatHanhMode => SelectedSearchTabIndex == 1;

    public bool IsThuaDatMode => SelectedSearchTabIndex == 2;

    public bool IsStatusVisible => IsSearching || IsInitializing;

    public string StatusSummary
    {
        get
        {
            if (IsInitializing)
            {
                return "Đang khởi tạo, vui lòng chờ...";
            }

            if (IsSearching)
            {
                return string.IsNullOrWhiteSpace(SearchProgress) ? "Đang tìm kiếm..." : SearchProgress;
            }

            return string.IsNullOrWhiteSpace(SearchProgress) ? "Sẵn sàng" : SearchProgress;
        }
    }

    private ObservableCollection<SearchResultModel> SearchResults { get; } = [];

    public CungCapThongTinViewModel(CungCapThongTinGiayChungNhanService searchService)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _searchService.StatusChanged += OnSearchServiceStatusChanged;
        var databaseService = new DatabaseService();
        _searchHistoryService = new SearchHistoryService(databaseService);
        _searchCacheService = new SearchCacheService(databaseService);
        InitializeRefreshOptions();
        _ = InitializeSearchHistoryAsync();
    }

    /// <summary>
    /// Đăng ký action để cập nhật DataGrid từ View
    /// </summary>
    public void RegisterDataGridUpdater(Action<AdvancedSearchGiayChungNhanResponse> updateAction)
    {
        _updateDataGridAction = updateAction;
    }

    /// <summary>
    /// Đăng ký action để cập nhật DataGrid kèm thời gian cache
    /// </summary>
    public void RegisterDataGridUpdater(Action<AdvancedSearchGiayChungNhanResponse, DateTime?> updateAction)
    {
        _updateDataGridActionWithCacheTime = updateAction;
    }

    partial void OnSelectedResultChanged(SearchResultModel? value)
    {
        if (value?.Response is { Data.Count: > 0 })
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

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStatusVisible));
        OnPropertyChanged(nameof(StatusSummary));
    }

    partial void OnIsInitializingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStatusVisible));
        OnPropertyChanged(nameof(StatusSummary));
    }

    partial void OnSearchProgressChanged(string value)
    {
        OnPropertyChanged(nameof(StatusSummary));
    }

    partial void OnSelectedSearchHistoryChanged(SearchHistoryEntry? value)
    {
        if (!string.IsNullOrWhiteSpace(value?.SearchQuery))
        {
            SearchInput = value.SearchQuery;
        }
    }

    partial void OnSelectedSearchTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSoGiayToMode));
        OnPropertyChanged(nameof(IsSoPhatHanhMode));
        OnPropertyChanged(nameof(IsThuaDatMode));
        SelectedSearchHistory = null;
        var searchTypeKey = GetSearchTypeKeyForTab(value);
        if (!string.IsNullOrWhiteSpace(searchTypeKey))
        {
            _ = LoadSearchHistoryAsync(searchTypeKey);
        }
    }

    private void OnSearchServiceStatusChanged(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        SearchProgress = message;
    }

    [RelayCommand]
    private async Task SearchBySoGiayToAsync()
    {
        Log.Information("SearchBySoGiayToAsync started");

        if (!string.IsNullOrWhiteSpace(SearchInput))
        {
            var input = SearchInput.Trim();
            var items = ParseInput(input, splitBySpace: true);
            Log.Information("Parsed {Count} items: {Items}", items.Length, string.Join(", ", items));

            try
            {
                var (alwaysRefresh, maxAge) = GetRefreshSettings();
                Log.Information("Starting PerformSearchAsync...");
                var foundCount = await PerformSearchAsync(items, async (item) =>
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        Log.Information("Empty item, skipping search");
                        return null;
                    }

                    var normalizedItem = item.Trim();
                    Log.Information("Searching for item: {Item}", normalizedItem);
                    var response = await _searchCacheService.GetOrSetAsync(
                        SearchTypeSoGiayToKey,
                        normalizedItem,
                        maxAge,
                        () => _searchService.SearchAsync(soGiayTo: normalizedItem),
                        alwaysRefresh);
                    var cachedAt = response is { Data.Count: > 0 }
                        ? await _searchCacheService.GetCachedAtAsync(SearchTypeSoGiayToKey, normalizedItem)
                        : null;
                    if (response is { Data.Count: > 0 })
                    {
                        Log.Information("SearchAsync returned {Count} results for item: {Item}", response.Data.Count,
                            item);
                        return new SearchExecutionResult(response, cachedAt);
                    }
                    else
                    {
                        normalizedItem = item.NormalizePersonalId();
                        if (normalizedItem == null)
                        {
                            Log.Information("Item normalization returned null for item: {Item}", normalizedItem);
                            return null;
                        }

                        Log.Information("Searching for item: {Item}", normalizedItem);
                        response = await _searchCacheService.GetOrSetAsync(
                            SearchTypeSoGiayToKey,
                            normalizedItem,
                            maxAge,
                            () => _searchService.SearchAsync(soGiayTo: normalizedItem),
                            alwaysRefresh);
                        cachedAt = response is { Data.Count: > 0 }
                            ? await _searchCacheService.GetCachedAtAsync(SearchTypeSoGiayToKey, normalizedItem)
                            : null;
                        if (response is { Data.Count: > 0 })
                        {
                            Log.Information("SearchAsync returned {Count} results for item: {Item}",
                                response.Data.Count, normalizedItem);
                            return new SearchExecutionResult(response, cachedAt);
                        }
                        else
                        {
                            Log.Information("Thử lại sau khi bỏ số 0 ở đầu cho item: {Item}", normalizedItem);
                            var modifiedItem = normalizedItem.TrimStart('0');
                            if (normalizedItem.Length == modifiedItem.Length)
                            {
                                Log.Information("No leading zeros to remove for item: {Item}", normalizedItem);
                                return null;
                            }

                            response = await _searchCacheService.GetOrSetAsync(
                                SearchTypeSoGiayToKey,
                                modifiedItem,
                                maxAge,
                                () => _searchService.SearchAsync(soGiayTo: modifiedItem),
                                alwaysRefresh);
                            cachedAt = response is { Data.Count: > 0 }
                                ? await _searchCacheService.GetCachedAtAsync(SearchTypeSoGiayToKey, modifiedItem)
                                : null;
                            if (response is { Data.Count: > 0 })
                            {
                                Log.Information("SearchAsync returned {Count} results for modified item: {Item}",
                                    response.Data.Count, modifiedItem);
                                return new SearchExecutionResult(response, cachedAt);
                            }
                            else
                            {
                                Log.Information("Thử lại sau khi bỏ 0 ở đầu cho item lần 2: {Item}", normalizedItem);
                                modifiedItem = modifiedItem.TrimStart('0');
                                if (normalizedItem.Length != modifiedItem.Length)
                                {
                                    response = await _searchCacheService.GetOrSetAsync(
                                        SearchTypeSoGiayToKey,
                                        modifiedItem,
                                        maxAge,
                                        () => _searchService.SearchAsync(soGiayTo: modifiedItem),
                                        alwaysRefresh);
                                    cachedAt = response is { Data.Count: > 0 }
                                        ? await _searchCacheService.GetCachedAtAsync(SearchTypeSoGiayToKey,
                                            modifiedItem)
                                        : null;
                                    return response is { Data.Count: > 0 }
                                        ? new SearchExecutionResult(response, cachedAt)
                                        : null;
                                }

                                Log.Information("No leading zeros to remove for item: {Item}", modifiedItem);
                                return null;
                            }
                        }
                    }
                }, "số giấy tờ");
                Log.Information("PerformSearchAsync completed");
                await SaveSearchHistoryAsync(SearchTypeSoGiayToKey, input, items.Length, foundCount);
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
        if (!string.IsNullOrWhiteSpace(SearchInput))
        {
            var (alwaysRefresh, maxAge) = GetRefreshSettings();
            var input = SearchInput.Trim();
            var items = ParseInput(input, splitBySpace: false);
            var foundCount = await PerformSearchAsync(items, async (item) =>
            {
                var modifiedItem = item.NormalizedSoPhatHanh();
                var response = await _searchCacheService.GetOrSetAsync(
                    SearchTypeSoPhatHanhKey,
                    modifiedItem,
                    maxAge,
                    () => _searchService.SearchAsync(soPhatHanh: modifiedItem),
                    alwaysRefresh);
                var cachedAt = response is { Data.Count: > 0 }
                    ? await _searchCacheService.GetCachedAtAsync(SearchTypeSoPhatHanhKey, modifiedItem)
                    : null;
                return response is { Data.Count: > 0 }
                    ? new SearchExecutionResult(response, cachedAt)
                    : null;
            }, "số phát hành");
            await SaveSearchHistoryAsync(SearchTypeSoPhatHanhKey, input, items.Length, foundCount);
        }
    }

    private static string[] ParseInput(string input, bool splitBySpace)
    {
        var separators = new HashSet<char> { '\n', '\r', ';' };

        return
        [
            .. SplitInput(input, separators, splitBySpace)
        ];
    }

    private static IEnumerable<string> SplitInput(string input, HashSet<char> separators, bool splitBySpace)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            yield break;
        }

        var buffer = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in input)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && (separators.Contains(ch) || (splitBySpace && char.IsWhiteSpace(ch))))
            {
                var token = buffer.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    yield return token;
                }

                buffer.Clear();
                continue;
            }

            buffer.Append(ch);
        }

        var last = buffer.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(last))
        {
            yield return last;
        }
    }

    private async Task SaveSearchHistoryAsync(
        string searchTypeKey,
        string input,
        int searchItemCount,
        int foundCount)
    {
        if (string.IsNullOrWhiteSpace(searchTypeKey))
        {
            return;
        }

        await _searchHistoryService.UpsertAsync(searchTypeKey, input, searchItemCount, foundCount, DateTime.Now);
        await LoadSearchHistoryAsync(searchTypeKey);
    }

    private async Task<int> PerformSearchAsync(
        string[] items,
        Func<string, Task<SearchExecutionResult?>> searchFunc,
        string searchType)
    {
        Log.Information("PerformSearchAsync called with {Count} items", items.Length);
        if (items.Length == 0) return 0;

        IsSearching = true;
        IsInitializing = true;
        ProgressMaximum = items.Length;
        ProgressValue = 0;
        ProgressPercentage = 0;
        CompletedItems = 0;
        TotalItems = items.Length;
        FoundItems = 0;
        CurrentSearchType = searchType;
        CurrentSearchItem = string.Empty;
        SearchProgress = "Đang khởi tạo, vui lòng chờ...";

        // Clear previous results
        SearchResults.Clear();

        // Clear DataGrid trước khi bắt đầu tìm kiếm
        ClearDataGridResults();

        Log.Information("Starting search loop...");
        Log.Information("Ensuring CungCapThongTin page...");
        try
        {
            await _searchService.EnsureCungCapThongTinPageAsync();
            IsInitializing = false;
            SearchProgress = $"Bắt đầu tìm {searchType}...";

            // Tổng hợp tất cả kết quả vào một response duy nhất
            var allData = new List<GiayChungNhanItem>();
            DateTime? oldestCacheTime = null;

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                CurrentSearchItem = item;
                SearchProgress = $"Đang tìm {searchType}: {item}";
                Log.Information("Calling search service for item: {Item}", item);

                try
                {
                    var result = await searchFunc(item);
                    Log.Information("Search service returned for item: {Item}, Result is null: {IsNull}", item,
                        result == null);

                    if (result?.Response != null)
                    {
                        var searchResult = new SearchResultModel
                        {
                            SearchQuery = item,
                            Response = result.Response,
                            SearchType = searchType,
                            SearchTime = DateTime.Now
                        };

                        // Add to results
                        SearchResults.Add(searchResult);

                        // Tổng hợp tất cả data[] vào danh sách chung
                        if (result.Response.Data.Count > 0)
                        {
                            allData.AddRange(result.Response.Data);
                            FoundItems = allData.Count;
                            SearchProgress = $"Tìm thấy: {item} - {result.Response.Data.Count} kết quả";
                            if (result.CachedAt.HasValue)
                            {
                                oldestCacheTime = oldestCacheTime.HasValue
                                    ? (result.CachedAt.Value < oldestCacheTime.Value
                                        ? result.CachedAt
                                        : oldestCacheTime)
                                    : result.CachedAt;
                            }

                            // Update DataGrid ngay lập tức với kết quả hiện tại
                            UpdateDataGridResults(allData, oldestCacheTime);
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

                CompletedItems = i + 1;
                ProgressValue = i + 1;
                ProgressPercentage = (ProgressValue / ProgressMaximum) * 100;

                // Small delay to show progress
                if (i < items.Length - 1)
                {
                    await Task.Delay(500);
                }
            }

            SearchProgress = $"Hoàn thành! Đã tìm {items.Length} mục, tìm thấy {FoundItems} kết quả.";

            // Cập nhật DataGrid lần cuối với tất cả kết quả (nếu chưa có kết quả nào)
            if (FoundItems > 0)
            {
                UpdateDataGridResults(allData, oldestCacheTime);
            }

            return FoundItems;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error trong PerformSearchAsync");
            SearchProgress = $"Lỗi: {ex.Message}";
            return FoundItems;
        }
        finally
        {
            CurrentSearchItem = string.Empty;
            CurrentSearchType = string.Empty;
            IsInitializing = false;
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
            Data = [],
            RecordsTotal = 0,
            RecordsFiltered = 0
        };
        _updateDataGridAction?.Invoke(emptyResponse);
        _updateDataGridActionWithCacheTime?.Invoke(emptyResponse, null);
    }

    /// <summary>
    /// Cập nhật kết quả vào DataGrid control
    /// </summary>
    private void UpdateDataGridResults(List<GiayChungNhanItem> allData, DateTime? oldestCacheTime)
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
        _updateDataGridActionWithCacheTime?.Invoke(combinedResponse, oldestCacheTime);
    }

    private async Task InitializeSearchHistoryAsync()
    {
        try
        {
            await _searchHistoryService.InitializeAsync();
            var searchTypeKey = GetSearchTypeKeyForTab(SelectedSearchTabIndex);
            if (!string.IsNullOrWhiteSpace(searchTypeKey))
            {
                await LoadSearchHistoryAsync(searchTypeKey);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize search history");
        }
    }

    private void InitializeRefreshOptions()
    {
        RefreshOptions =
        [
            new CacheRefreshOption("Luôn luôn", null),
            new CacheRefreshOption("1 ngày", 1),
            new CacheRefreshOption("2 ngày", 2),
            new CacheRefreshOption("3 ngày", 3),
            new CacheRefreshOption("5 ngày", 5),
            new CacheRefreshOption("7 ngày", 7)
        ];

        SelectedRefreshOption = RefreshOptions.FirstOrDefault(option => option.Days == 7) ?? RefreshOptions.Last();
    }

    private (bool alwaysRefresh, TimeSpan maxAge) GetRefreshSettings()
    {
        var option = SelectedRefreshOption ?? RefreshOptions.LastOrDefault();
        if (option == null || option.IsAlways)
        {
            return (true, TimeSpan.Zero);
        }

        return (false, option.GetMaxAge());
    }

    private sealed record SearchExecutionResult(
        AdvancedSearchGiayChungNhanResponse Response,
        DateTime? CachedAt);

    private async Task LoadSearchHistoryAsync(string searchTypeKey)
    {
        var history = await _searchHistoryService.GetHistoryAsync(searchTypeKey);
        SearchHistory.Clear();
        SearchInput = string.Empty;
        foreach (var entry in history)
        {
            SearchHistory.Add(entry);
        }
    }

    [RelayCommand]
    private void StartEditHistory(SearchHistoryEntry? entry)
    {
        if (entry == null)
        {
            return;
        }

        foreach (var item in SearchHistory)
        {
            item.IsEditing = false;
        }

        entry.EditingTitle = string.IsNullOrWhiteSpace(entry.Title) ? entry.DefaultTitle : entry.Title;
        entry.IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveHistoryTitleAsync(SearchHistoryEntry? entry)
    {
        if (entry == null)
        {
            return;
        }

        var title = entry.EditingTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title == entry.DefaultTitle)
        {
            title = null;
        }

        entry.Title = title;
        entry.IsEditing = false;
        await _searchHistoryService.UpdateTitleAsync(entry.Id, title);
    }

    [RelayCommand]
    private void CancelEditHistory(SearchHistoryEntry? entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.EditingTitle = entry.Title;
        entry.IsEditing = false;
    }

    [RelayCommand]
    private async Task SearchFromHistoryAsync(SearchHistoryEntry? entry)
    {
        if (entry == null)
        {
            return;
        }

        SearchInput = entry.SearchQuery;
        switch (entry.SearchType)
        {
            case SearchTypeSoGiayToKey:
                SelectedSearchTabIndex = 0;
                await SearchBySoGiayToAsync();
                break;
            case SearchTypeSoPhatHanhKey:
                SelectedSearchTabIndex = 1;
                await SearchBySoPhatHanhAsync();
                break;
            case SearchTypeThuaDatKey:
                SelectedSearchTabIndex = 2;
                break;
        }
    }

    private static string GetSearchTypeKeyForTab(int selectedTabIndex)
    {
        return selectedTabIndex switch
        {
            0 => SearchTypeSoGiayToKey,
            1 => SearchTypeSoPhatHanhKey,
            2 => SearchTypeThuaDatKey,
            _ => string.Empty
        };
    }
}