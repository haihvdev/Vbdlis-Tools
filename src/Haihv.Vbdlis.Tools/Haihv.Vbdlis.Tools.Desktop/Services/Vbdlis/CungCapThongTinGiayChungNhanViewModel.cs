using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Extensions;
using Haihv.Vbdlis.Tools.Desktop.Models;
using Haihv.Vbdlis.Tools.Desktop.Services;
using Microsoft.Playwright;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services.Vbdlis;

/// <summary>
/// Service cho tìm kiếm nâng cao Giấy chứng nhận trên VBDLIS sử dụng Playwright
/// </summary>
public class CungCapThongTinGiayChungNhanService(
    IPlaywrightService playwrightService,
    LoginSessionInfo loginSessionInfo)
{
    private readonly ILogger _logger = Log.ForContext<CungCapThongTinGiayChungNhanService>();
    private readonly IPlaywrightService _playwrightService = playwrightService;
    private readonly LoginSessionInfo _loginSessionInfo = loginSessionInfo;
    private IPage? _page;

    /// <summary>
    /// Gọi API tìm kiếm nâng cao Giấy chứng nhận theo số phát hành hoặc số giấy tờ
    /// </summary>
    public async Task<AdvancedSearchGiayChungNhanResponse?> SearchAsync(
        string? soPhatHanh = null,
        string? soGiayTo = null)
    {
        if (string.IsNullOrWhiteSpace(soPhatHanh) && string.IsNullOrWhiteSpace(soGiayTo))
        {
            throw new ArgumentNullException(nameof(soPhatHanh),
                "Số phát hành hoặc Số giấy tờ phải được cung cấp để tìm kiếm.");
        }

        await EnsureCungCapThongTinPageAsync();
        var formData = CungCapThongTinGiayChungNhanPayload.GetAdvancedSearchGiayChungNhanPayload(
            soPhatHanh, soGiayTo);
        var json = await PostAdvancedSearchAsync(formData);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var response = AdvancedSearchGiayChungNhanResponse.FromJson(json);
            if (response == null)
            {
                _logger.Warning("Advanced search Giấy chứng nhận trả về null.");
            }
            return response;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.Error(ex, "Lỗi parse kết quả advanced search Giấy chứng nhận.");
            Debug.WriteLine($"AdvancedSearchGCN Parse Error: {ex.Message}");
            Debug.WriteLine(json);
            return null;
        }
    }

    /// <summary>
    /// Gọi API tìm kiếm nâng cao Giấy chứng nhận theo đơn vị hành chính và Tờ bản đồ, thửa đất
    /// </summary>
    public async Task<AdvancedSearchGiayChungNhanResponse?> SearchAsync(
        int thuTuThua,
        int toBanDo,
        int xaId,
        int tinhId = 24)
    {
        tinhId = tinhId <= 0 ? 24 : tinhId;
        if (xaId <= 0 || thuTuThua <= 0 || toBanDo <= 0)
        {
            throw new ArgumentNullException(nameof(xaId),
                "Đơn vị hành chính, Tờ bản đồ và Thửa đất phải được cung cấp để tìm kiếm.");
        }

        await EnsureCungCapThongTinPageAsync();
        var formData = CungCapThongTinGiayChungNhanPayload.GetAdvancedSearchGiayChungNhanPayload(
            thuTuThua, toBanDo, xaId, tinhId);
        var json = await PostAdvancedSearchAsync(formData);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var response = AdvancedSearchGiayChungNhanResponse.FromJson(json);
            if (response == null)
            {
                _logger.Warning("Advanced search Giấy chứng nhận trả về null.");
            }
            return response;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.Error(ex, "Lỗi parse kết quả advanced search Giấy chứng nhận.");
            Debug.WriteLine($"AdvancedSearchGCN Parse Error: {ex.Message}");
            Debug.WriteLine(json);
            return null;
        }
    }

    /// <summary>
    /// Gọi AJAX POST request sử dụng Playwright
    /// </summary>
    private async Task<string> PostAdvancedSearchAsync(string formData)
    {
        if (_page == null)
        {
            throw new InvalidOperationException("Page not initialized. Call EnsureCungCapThongTinPageAsync first.");
        }

        var sanitizedFormData = formData.Replace("\r", "").Replace("\n", "");

        const string script = """
            async ([url, payload]) => {
                if (typeof $ === 'undefined' || typeof $.ajax === 'undefined') {
                    return JSON.stringify({ error: 'jQuery not available on page' });
                }

                return new Promise((resolve) => {
                    $.ajax({
                        url: url,
                        type: 'POST',
                        data: payload,
                        contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
                        timeout: 120000,
                        success: function(data) {
                            resolve(typeof data === 'object' ? JSON.stringify(data) : data);
                        },
                        error: function(xhr, status, error) {
                            resolve(JSON.stringify({
                                error: error,
                                status: xhr.status,
                                statusText: status,
                                responseText: xhr.responseText
                            }));
                        }
                    });
                });
            }
            """;

        try
        {
            var response = await _page.EvaluateAsync<string>(
                script,
                new object[] { _loginSessionInfo.AdvancedSearchGiayChungNhanUrl, sanitizedFormData });

            Debug.WriteLine($"AdvancedSearchGCN: Response length {response?.Length ?? 0}");
            return response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lỗi gọi AdvancedSearchGiayChungNhan");
            Debug.WriteLine($"AdvancedSearchGCN EvaluateAsync Error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gọi API lấy Giấy chứng nhận biến động
    /// </summary>
    /// <param name="giayChungNhanId">ID của Giấy chứng nhận cần lấy thông tin biến động</param>
    /// <returns>Thông tin Giấy chứng nhận biến động</returns>
    public async Task<GetGiayChungNhanBienDongResponse?> GetGiayChungNhanBienDong(long giayChungNhanId)
    {
        if (giayChungNhanId <= 0)
        {
            throw new ArgumentException("Giấy chứng nhận ID phải lớn hơn 0.", nameof(giayChungNhanId));
        }

        await EnsureCungCapThongTinPageAsync();
        var json = await PostGetGiayChungNhanBienDongAsync(giayChungNhanId);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var response = GetGiayChungNhanBienDongResponse.FromJson(json);
            if (response == null)
            {
                _logger.Warning("GetGiayChungNhanBienDong trả về null.");
            }
            return response;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.Error(ex, "Lỗi parse kết quả GetGiayChungNhanBienDong.");
            Debug.WriteLine($"GetGiayChungNhanBienDong Parse Error: {ex.Message}");
            Debug.WriteLine(json);
            return null;
        }
    }

    /// <summary>
    /// Gọi AJAX POST request cho GetGiayChungNhanBienDong sử dụng Playwright
    /// </summary>
    private async Task<string> PostGetGiayChungNhanBienDongAsync(long giayChungNhanId)
    {
        if (_page == null)
        {
            throw new InvalidOperationException("Page not initialized. Call EnsureCungCapThongTinPageAsync first.");
        }

        const string script = """
            async ([url, giayChungNhanId]) => {
                if (typeof $ === 'undefined' || typeof $.ajax === 'undefined') {
                    return JSON.stringify({ error: 'jQuery not available on page' });
                }

                return new Promise((resolve) => {
                    $.ajax({
                        url: url,
                        type: 'POST',
                        data: { giayChungNhanId: giayChungNhanId },
                        contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
                        timeout: 120000,
                        success: function(data) {
                            resolve(typeof data === 'object' ? JSON.stringify(data) : data);
                        },
                        error: function(xhr, status, error) {
                            resolve(JSON.stringify({
                                error: error,
                                status: xhr.status,
                                statusText: status,
                                responseText: xhr.responseText
                            }));
                        }
                    });
                });
            }
            """;

        try
        {
            var response = await _page.EvaluateAsync<string>(
                script,
                new object[] { _loginSessionInfo.GetGiayChungNhanBienDongUrl, giayChungNhanId });

            Debug.WriteLine($"GetGiayChungNhanBienDong: Response length {response?.Length ?? 0}");
            return response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lỗi gọi GetGiayChungNhanBienDong");
            Debug.WriteLine($"GetGiayChungNhanBienDong EvaluateAsync Error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gọi API lấy thông tin tập tin hồ sơ quét
    /// </summary>
    /// <param name="lstNodeId">Danh sách Node ID của các tập tin</param>
    /// <param name="hoSoQuetId">ID của hồ sơ quét</param>
    /// <param name="checkPermission">Kiểm tra quyền truy cập (mặc định: true)</param>
    /// <returns>Danh sách thông tin tập tin hồ sơ quét</returns>
    public async Task<GetThongTinTapTinHoSoQuetsResponse?> GetThongTinTapTinHoSoQuets(
        List<string> lstNodeId,
        long hoSoQuetId,
        bool checkPermission = true)
    {
        if (lstNodeId == null || lstNodeId.Count == 0)
        {
            throw new ArgumentException("Danh sách Node ID không được rỗng.", nameof(lstNodeId));
        }

        if (hoSoQuetId <= 0)
        {
            throw new ArgumentException("Hồ sơ quét ID phải lớn hơn 0.", nameof(hoSoQuetId));
        }

        await EnsureCungCapThongTinPageAsync();
        var json = await PostGetThongTinTapTinHoSoQuetsAsync(lstNodeId, hoSoQuetId, checkPermission);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var response = GetThongTinTapTinHoSoQuetsResponse.FromJson(json);
            if (response == null)
            {
                _logger.Warning("GetThongTinTapTinHoSoQuets trả về null.");
            }
            return response;
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Lỗi parse kết quả GetThongTinTapTinHoSoQuets.");
            Debug.WriteLine($"GetThongTinTapTinHoSoQuets Parse Error: {ex.Message}");
            Debug.WriteLine(json);
            return null;
        }
    }

    /// <summary>
    /// Gọi AJAX POST request cho GetThongTinTapTinHoSoQuets sử dụng Playwright
    /// </summary>
    private async Task<string> PostGetThongTinTapTinHoSoQuetsAsync(
        List<string> lstNodeId,
        long hoSoQuetId,
        bool checkPermission)
    {
        if (_page == null)
        {
            throw new InvalidOperationException("Page not initialized. Call EnsureCungCapThongTinPageAsync first.");
        }

        const string script = """
            async ([url, payload]) => {
                if (typeof $ === 'undefined' || typeof $.ajax === 'undefined') {
                    return JSON.stringify({ error: 'jQuery not available on page' });
                }

                return new Promise((resolve) => {
                    $.ajax({
                        url: url,
                        type: 'POST',
                        data: JSON.stringify(payload),
                        contentType: 'application/json; charset=UTF-8',
                        timeout: 120000,
                        success: function(data) {
                            resolve(typeof data === 'object' ? JSON.stringify(data) : data);
                        },
                        error: function(xhr, status, error) {
                            resolve(JSON.stringify({
                                error: error,
                                status: xhr.status,
                                statusText: status,
                                responseText: xhr.responseText
                            }));
                        }
                    });
                });
            }
            """;

        try
        {
            var payload = new
            {
                lstNodeId = lstNodeId,
                checkPermission = checkPermission,
                hoSoQuetId = hoSoQuetId
            };

            var response = await _page.EvaluateAsync<string>(
                script,
                new object[] { _loginSessionInfo.GetThongTinTapTinHoSoQuetsUrl, payload });

            Debug.WriteLine($"GetThongTinTapTinHoSoQuets: Response length {response?.Length ?? 0}");
            return response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lỗi gọi GetThongTinTapTinHoSoQuets");
            Debug.WriteLine($"GetThongTinTapTinHoSoQuets EvaluateAsync Error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Đảm bảo đã mở trang cung cấp thông tin GCN
    /// </summary>
    public async Task EnsureCungCapThongTinPageAsync()
    {
        if (!_playwrightService.IsInitialized)
        {
            await _playwrightService.InitializeAsync();
        }

        // Tạo page mới nếu chưa có
        if (_page == null)
        {
            _page = await _playwrightService.NewPageAsync();
        }

        try
        {
            // Kiểm tra nếu page đã ở đúng URL
            var currentUrl = _page.Url;
            var targetUrl = _loginSessionInfo.CungCapThongTinGiayChungNhanPageUrl;

            if (!string.IsNullOrEmpty(currentUrl) && currentUrl.StartsWith(targetUrl))
            {
                _logger.Debug("Already on CungCapThongTin page");
                return;
            }

            // Navigate đến trang
            await _page.GotoAsync(targetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 60000
            });

            _logger.Information("Navigated to CungCapThongTin page: {Url}", targetUrl);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to navigate to CungCapThongTin page");
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
