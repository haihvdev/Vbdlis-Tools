using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Haihv.Vbdlis.Tools.Desktop.Services.Data;
using Microsoft.Playwright;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels;

/// <summary>
/// ViewModel quản lý đơn vị hành chính (ĐVHC) sử dụng Playwright
/// </summary>
public class DonViHanhChinhViewModel
{
    private readonly ILogger _logger = Log.ForContext<DonViHanhChinhViewModel>();
    private readonly IPlaywrightService _playwrightService;
    private readonly LoginSessionInfo _loginSessionInfo;
    private readonly DvhcService _dvhcCacheService;
    private readonly ThamChieuToBanDoService _thamChieuToBanDoService;
    private readonly DvhcElementSelectors _elementSelectors;
    private IPage? _page;
    private int _currentTinhId;
    private int _currentHuyenId;

    public const int CleanupInterval = 10; // Cleanup memory sau mỗi 10 uploads

    public DonViHanhChinhViewModel(
        IPlaywrightService playwrightService,
        LoginSessionInfo loginSessionInfo,
        DvhcElementSelectors? elementSelectors = null)
    {
        _playwrightService = playwrightService;
        _loginSessionInfo = loginSessionInfo;
        var databaseService = new DatabaseService();
        _thamChieuToBanDoService = new ThamChieuToBanDoService(databaseService);
        _dvhcCacheService = new DvhcService(databaseService);
        _elementSelectors = elementSelectors ?? new DvhcElementSelectors();
    }

    /// <summary>
    /// Khởi tạo service tham chiếu tờ bản đồ
    /// </summary>
    public async Task InitializeThamChieuToBanDoAsync()
        => await _thamChieuToBanDoService.InitializeAsync();

    /// <summary>
    /// Tìm element theo danh sách ID sử dụng Playwright
    /// </summary>
    private async Task<IElementHandle?> FindElementByIdsAsync(
        IReadOnlyList<string> possibleIds,
        bool requireVisible = false,
        bool requireEnabled = false)
    {
        if (_page == null) return null;

        foreach (var elementId in possibleIds)
        {
            try
            {
                var element = await _page.QuerySelectorAsync($"#{elementId}");
                if (element == null) continue;

                if (requireVisible)
                {
                    var isVisible = await element.IsVisibleAsync();
                    if (!isVisible) continue;
                }

                if (requireEnabled)
                {
                    var isEnabled = await element.IsEnabledAsync();
                    if (!isEnabled) continue;
                }

                return element;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DonViHanhChinhViewModel: Element with ID {elementId} error: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Đảm bảo đã mở trang cung cấp thông tin GCN
    /// </summary>
    public async Task EnsurePageAsync()
    {
        if (!_playwrightService.IsInitialized)
        {
            await _playwrightService.InitializeAsync();
        }

        // Tạo page mới nếu chưa có
        _page ??= await _playwrightService.NewPageAsync();

        try
        {
            // Kiểm tra nếu page đã ở đúng URL
            var currentUrl = _page.Url;
            var targetUrl = _loginSessionInfo.CungCapThongTinGiayChungNhanPageUrl;

            if (!string.IsNullOrEmpty(currentUrl) && currentUrl.StartsWith(targetUrl))
            {
                _logger.Debug("Already on the target page");
                return;
            }

            // Navigate đến trang
            await _page.GotoAsync(targetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 60000
            });

            // Đợi select element xuất hiện
            var huyenElement = await FindElementByIdsAsync(_elementSelectors.HuyenIds, requireVisible: true);
            if (huyenElement is null)
            {
                throw new TimeoutException("Không tìm thấy dropdown Quận/Huyện trên trang web.");
            }

            await GetCapTinhAsync();
            _logger.Information("Navigated to page: {Url}", targetUrl);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to navigate to page");
            throw;
        }
    }

    /// <summary>
    /// Điều hướng đến trang upload HSQ
    /// </summary>
    public async Task NavigateToUploadPageAsync()
    {
        await EnsurePageAsync();
    }

    /// <summary>
    /// Lấy thông tin mã Tỉnh/Thành phố từ select box trên trang web
    /// </summary>
    private async Task<int> GetCapTinhAsync(bool forceReload = false)
    {
        try
        {
            if (!forceReload && _currentTinhId > 0)
            {
                return _currentTinhId;
            }

            if (_page == null)
            {
                throw new InvalidOperationException("Page not initialized. Call EnsurePageAsync first.");
            }

            Debug.WriteLine("DonViHanhChinhViewModel: Getting province from select box...");

            var selectElement = await FindElementByIdsAsync(_elementSelectors.TinhIds);
            if (selectElement is null)
            {
                throw new TimeoutException("Không tìm thấy dropdown Tỉnh/Thành phố trên trang web.");
            }

            var valueStr = await selectElement.EvaluateAsync<string>("el => el.value");
            _currentTinhId = int.TryParse(valueStr, out var value) && value > 0 ? value : 0;

            return _currentTinhId;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DonViHanhChinhViewModel GetCapTinhAsync Error");
            Debug.WriteLine($"DonViHanhChinhViewModel GetCapTinhAsync Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Lấy danh sách Quận/Huyện từ select box trên trang web
    /// </summary>
    public async Task<List<DvhcCapHuyen>> GetCapHuyenAsync(bool forceReload = false, int retryCount = 0)
    {
        try
        {
            var currentTinhId = await GetCapTinhAsync(forceReload);
            var capHuyen = new List<DvhcCapHuyen>();

            if (!forceReload && currentTinhId > 0)
            {
                capHuyen = await _dvhcCacheService.GetCapHuyenListAsync(currentTinhId);
                if (capHuyen.Count > 0)
                {
                    Debug.WriteLine(
                        $"DonViHanhChinhViewModel: Retrieved {capHuyen.Count} districts from cache for provinceId={currentTinhId}");
                    return capHuyen;
                }
            }

            if (_page == null)
            {
                throw new InvalidOperationException("Page not initialized. Call EnsurePageAsync first.");
            }

            Debug.WriteLine("DonViHanhChinhViewModel: Getting districts from select box...");

            var selectElement = await FindElementByIdsAsync(_elementSelectors.HuyenIds, requireVisible: true, requireEnabled: true);

            if (selectElement == null)
            {
                // Retry một lần
                if (retryCount == 0)
                {
                    Debug.WriteLine("DonViHanhChinhViewModel: Retrying GetCapHuyenAsync");
                    await NavigateToUploadPageAsync();
                    return await GetCapHuyenAsync(forceReload, retryCount + 1);
                }

                throw new TimeoutException("Không tìm thấy dropdown Quận/Huyện trên trang web.");
            }

            var options = await selectElement.EvaluateAsync<List<Dictionary<string, string>>>(@"
                el => Array.from(el.options).map(opt => ({
                    value: opt.value,
                    text: opt.text
                }))
            ");

            Debug.WriteLine($"DonViHanhChinhViewModel: Found {options.Count} options");

            foreach (var option in options)
            {
                var valueStr = option["value"];
                var text = option["text"];

                if (int.TryParse(valueStr, out var value) && value > 0 && !string.IsNullOrWhiteSpace(text))
                {
                    capHuyen.Add(new DvhcCapHuyen
                    {
                        Id = value,
                        Name = text.Trim(),
                        CapTinhId = currentTinhId
                    });
                }
            }

            // Fire and forget cache save
            var saveTask = _dvhcCacheService.SaveCapHuyenListAsync(currentTinhId, capHuyen);
            Debug.WriteLine($"DonViHanhChinhViewModel: Successfully retrieved {capHuyen.Count} districts");
            return capHuyen;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DonViHanhChinhViewModel GetCapHuyenAsync Error");
            Debug.WriteLine($"DonViHanhChinhViewModel GetCapHuyenAsync Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Chọn Quận/Huyện trên trang web để trigger update danh sách Phường/Xã
    /// </summary>
    public async Task SelectCapHuyenAsync(int huyenId, int retryCount = 0)
    {
        try
        {
            if (_page == null)
            {
                throw new InvalidOperationException("Page not initialized. Call EnsurePageAsync first.");
            }

            Debug.WriteLine($"DonViHanhChinhViewModel: Selecting district with ID: {huyenId}");

            var selectElement = await FindElementByIdsAsync(_elementSelectors.HuyenIds, requireVisible: true, requireEnabled: true);

            if (selectElement == null)
            {
                // Retry một lần
                if (retryCount == 0)
                {
                    Debug.WriteLine("DonViHanhChinhViewModel: Retrying SelectCapHuyenAsync");
                    await NavigateToUploadPageAsync();
                    await SelectCapHuyenAsync(huyenId, retryCount + 1);
                    return;
                }

                throw new InvalidOperationException("Không tìm thấy dropdown Quận/Huyện.");
            }

            // Chọn huyện
            await selectElement.SelectOptionAsync(huyenId.ToString());

            Debug.WriteLine(
                $"DonViHanhChinhViewModel: Selected district ID {huyenId}, waiting for ward dropdown to update...");

            // Đợi ward dropdown cập nhật
            await _page.WaitForFunctionAsync(@"
                (xaIds) => {
                    for (const id of xaIds) {
                        const select = document.getElementById(id);
                        if (select && select.enabled && select.options.length > 1) {
                            return true;
                        }
                    }
                    return false;
                }
            ", _elementSelectors.XaIds, new PageWaitForFunctionOptions { Timeout = 30000 });

            Debug.WriteLine("DonViHanhChinhViewModel: Ward dropdown updated successfully");

            // Lưu lại ID huyện hiện tại
            _currentHuyenId = huyenId;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DonViHanhChinhViewModel SelectCapHuyenAsync Error");
            Debug.WriteLine($"DonViHanhChinhViewModel SelectCapHuyenAsync Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Lấy danh sách Phường/Xã từ select box trên trang web
    /// </summary>
    public async Task<List<DvhcCapXa>> GetCapXaAsync(bool forceReload = false, int retryCount = 0)
    {
        try
        {
            var currentTinhId = await GetCapTinhAsync(forceReload);
            List<DvhcCapXa> capXa = [];

            // Chỉ load từ cache khi KHÔNG có _currentHuyenId (tức là chưa select huyện cụ thể)
            // Nếu đã select huyện (_currentHuyenId > 0), phải đọc từ dropdown để lấy đúng xã của huyện đó
            if (!forceReload && currentTinhId > 0 && _currentHuyenId == 0)
            {
                capXa = await _dvhcCacheService.GetCapXaListByTinhAsync(currentTinhId);
                if (capXa.Count > 0)
                {
                    Debug.WriteLine(
                        $"DonViHanhChinhViewModel: Retrieved {capXa.Count} wards from cache for provinceId={currentTinhId}");
                    return capXa;
                }
            }
            else if (_currentHuyenId > 0)
            {
                Debug.WriteLine(
                    $"DonViHanhChinhViewModel: Skipping cache because district was selected (HuyenId={_currentHuyenId}), will read from dropdown");
            }

            if (_page == null)
            {
                throw new InvalidOperationException("Page not initialized. Call EnsurePageAsync first.");
            }

            Debug.WriteLine("DonViHanhChinhViewModel: Getting wards from select box...");

            var selectElement = await FindElementByIdsAsync(_elementSelectors.XaIds, requireVisible: true, requireEnabled: true);

            if (selectElement == null)
            {
                // Retry một lần
                if (retryCount == 0)
                {
                    Debug.WriteLine("DonViHanhChinhViewModel: Retrying GetCapXaAsync");
                    await NavigateToUploadPageAsync();
                    return await GetCapXaAsync(forceReload, retryCount + 1);
                }

                throw new TimeoutException("Không tìm thấy dropdown Phường/Xã trên trang web.");
            }

            Debug.WriteLine("DonViHanhChinhViewModel: Ward select element found and ready");

            var options = await selectElement.EvaluateAsync<List<Dictionary<string, string>>>(@"
                el => Array.from(el.options).map(opt => ({
                    value: opt.value,
                    text: opt.text
                }))
            ");

            Debug.WriteLine($"DonViHanhChinhViewModel: Found {options.Count} ward options");

            foreach (var option in options)
            {
                var valueStr = option["value"];
                var text = option["text"];

                if (!string.IsNullOrWhiteSpace(valueStr) && int.TryParse(valueStr, out var value) && value > 0 &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    capXa.Add(new DvhcCapXa
                    {
                        Id = value,
                        Name = text.Trim(),
                        CapTinhId = currentTinhId,
                        CapHuyenId = _currentHuyenId // Lưu ID huyện hiện tại
                    });
                }
            }

            // Fire and forget cache save
            var saveTask = _dvhcCacheService.SaveCapXaListAsync(currentTinhId, capXa);
            Debug.WriteLine($"DonViHanhChinhViewModel: Successfully retrieved {capXa.Count} wards");
            return capXa;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DonViHanhChinhViewModel GetCapXaAsync Error");
            Debug.WriteLine($"DonViHanhChinhViewModel GetCapXaAsync Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Đóng page hiện tại
    /// </summary>
    public async Task ClosePageAsync()
    {
        if (_page != null)
        {
            await _page.CloseAsync();
            _page = null;
        }
    }
}

/// <summary>
/// Cấu hình element selectors cho các dropdown ĐVHC
/// </summary>
public class DvhcElementSelectors(
    IEnumerable<string>? tinhIds = null,
    IEnumerable<string>? huyenIds = null,
    IEnumerable<string>? xaIds = null)
{
    private static readonly string[] DefaultTinhIds = ["ddlTinhThanhKeKhai", "ddlTinhThanh"];
    private static readonly string[] DefaultHuyenIds = ["ddlQuanHuyenKeKhai", "ddlQuanHuyen"];
    private static readonly string[] DefaultXaIds = ["ddlPhuongXaKeKhai", "ddlPhuongXa"];

    public IReadOnlyList<string> TinhIds { get; } = BuildIds(tinhIds, DefaultTinhIds);
    public IReadOnlyList<string> HuyenIds { get; } = BuildIds(huyenIds, DefaultHuyenIds);
    public IReadOnlyList<string> XaIds { get; } = BuildIds(xaIds, DefaultXaIds);

    private static IReadOnlyList<string> BuildIds(IEnumerable<string>? ids, IReadOnlyList<string> fallback)
    {
        var cleanedIds = ids?.Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleanedIds is { Length: > 0 } ? cleanedIds : fallback;
    }
}
