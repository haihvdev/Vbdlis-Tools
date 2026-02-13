using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using Haihv.Vbdlis.Tools.Api.Playwright.Models;
using Haihv.Vbdlis.Tools.Api.Playwright.Options;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Haihv.Vbdlis.Tools.Api.Playwright.Services;

public sealed class VbdlisPlaywrightSearchService : IVbdlisPlaywrightSearchService, IAsyncDisposable
{
    private readonly PlaywrightSettings _playwrightSettings;
    private readonly VbdlisSettings _vbdlisSettings;
    private readonly ILogger<VbdlisPlaywrightSearchService> _logger;
    private readonly SemaphoreSlim _playwrightInitLock = new(1, 1);
    private readonly SemaphoreSlim _sessionCreateLock = new(1, 1);

    private readonly ConcurrentDictionary<string, UserSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private IPlaywright? _playwright;
    private bool _disposed;

    public VbdlisPlaywrightSearchService(
        IOptions<PlaywrightSettings> playwrightOptions,
        IOptions<VbdlisSettings> vbdlisOptions,
        ILogger<VbdlisPlaywrightSearchService> logger)
    {
        _playwrightSettings = playwrightOptions.Value;
        _vbdlisSettings = vbdlisOptions.Value;
        _logger = logger;
    }

    public async Task<VbdlisBatchSearchResponse> SearchAsync(VbdlisBatchSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();

        var mode = request.ResponseMode;
        var soGiayToList = request.SoGiayToList
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (soGiayToList.Count == 0)
        {
            throw new ArgumentException("soGiayToList không được để trống.", nameof(request.SoGiayToList));
        }

        var serverUrl = ResolveServerUrl(request.Server);
        var authHost = TryGetHost(_vbdlisSettings.AuthenUrl);

        await CleanupExpiredSessionsAsync(cancellationToken);

        var session = await EnsureSessionAsync(
            serverUrl,
            request.Username,
            request.Headless ?? _playwrightSettings.Headless,
            cancellationToken);

        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await EnsurePageAsync(session, cancellationToken);
            await EnsureLoggedInAsync(page, serverUrl, request.Username, request.Password, authHost, cancellationToken);

            var searchPageUrl = BuildSearchPageUrl(serverUrl);
            await EnsureSearchPageAsync(page, searchPageUrl, cancellationToken);

            var searchUrl = BuildAdvancedSearchUrl(serverUrl);
            var results = new List<VbdlisSearchResultItem>(soGiayToList.Count);

            foreach (var soGiayTo in soGiayToList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var payload = BuildAdvancedSearchPayload(soGiayTo, request.TinhId ?? 24);
                    var rawJson = await PostAdvancedSearchAsync(page, searchUrl, payload);

                    if (string.IsNullOrWhiteSpace(rawJson))
                    {
                        results.Add(new VbdlisSearchResultItem
                        {
                            SoGiayTo = soGiayTo,
                            Success = false,
                            Error = "VBDLIS không trả dữ liệu."
                        });
                        continue;
                    }

                    var node = JsonNode.Parse(rawJson);
                    if (node is not JsonObject obj)
                    {
                        results.Add(new VbdlisSearchResultItem
                        {
                            SoGiayTo = soGiayTo,
                            Success = false,
                            Error = "Dữ liệu VBDLIS không đúng định dạng JSON object."
                        });
                        continue;
                    }

                    var statusText = GetString(obj, "statusText");
                    var hasError = !string.IsNullOrWhiteSpace(statusText)
                                   && statusText.Contains("error", StringComparison.OrdinalIgnoreCase);
                    var summarySource = mode is VbdlisResponseMode.Summary or VbdlisResponseMode.Compact
                        ? ExtractSummary(obj)
                        : null;
                    var summaryData = mode == VbdlisResponseMode.Summary ? summarySource : null;
                    var compactData = mode == VbdlisResponseMode.Compact && summarySource != null
                        ? BuildCompactData(summarySource)
                        : null;

                    results.Add(new VbdlisSearchResultItem
                    {
                        SoGiayTo = soGiayTo,
                        Success = !hasError,
                        Error = hasError ? statusText : null,
                        RecordsTotal = GetNullableInt(obj, "recordsTotal"),
                        RecordsFiltered = GetNullableInt(obj, "recordsFiltered"),
                        FullData = mode == VbdlisResponseMode.Full ? obj : null,
                        SummaryData = summaryData,
                        CompactData = compactData
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Search failed for soGiayTo={SoGiayTo}, username={Username}", soGiayTo,
                        request.Username);
                    results.Add(new VbdlisSearchResultItem
                    {
                        SoGiayTo = soGiayTo,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            session.LastAccessUtc = DateTimeOffset.UtcNow;

            return new VbdlisBatchSearchResponse
            {
                Mode = mode switch
                {
                    VbdlisResponseMode.Compact => "compact",
                    VbdlisResponseMode.Summary => "summary",
                    VbdlisResponseMode.Full => "full",
                    _ => "summary"
                },
                Results = results
            };
        }
        finally
        {
            session.LastAccessUtc = DateTimeOffset.UtcNow;
            session.Gate.Release();
        }
    }

    private async Task<UserSession> EnsureSessionAsync(string serverUrl, string username, bool headless,
        CancellationToken cancellationToken)
    {
        var key = BuildSessionKey(serverUrl, username);

        if (_sessions.TryGetValue(key, out var existing))
        {
            _logger.LogDebug("Reusing Playwright session for key={SessionKey}", key);
            return existing;
        }

        await _sessionCreateLock.WaitAsync(cancellationToken);
        try
        {
            if (_sessions.TryGetValue(key, out existing))
            {
                _logger.LogDebug("Reusing Playwright session for key={SessionKey}", key);
                return existing;
            }

            var playwright = await EnsurePlaywrightAsync(cancellationToken);
            var userDataDir = BuildUserDataDir(serverUrl, username);
            Directory.CreateDirectory(userDataDir);

            var context = await LaunchPersistentContextAsync(playwright, userDataDir, headless);
            var session = new UserSession(
                key,
                serverUrl,
                username,
                headless,
                userDataDir,
                context,
                new SemaphoreSlim(1, 1),
                DateTimeOffset.UtcNow);

            _sessions[key] = session;
            _logger.LogInformation("Created new Playwright session for key={SessionKey}, path={UserDataDir}", key,
                userDataDir);
            return session;
        }
        finally
        {
            _sessionCreateLock.Release();
        }
    }

    private async Task<IBrowserContext> LaunchPersistentContextAsync(IPlaywright playwright, string userDataDir,
        bool headless)
    {
        try
        {
            return await playwright.Chromium.LaunchPersistentContextAsync(userDataDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = headless,
                    SlowMo = _playwrightSettings.SlowMo,
                    Locale = "vi-VN",
                    TimezoneId = "Asia/Ho_Chi_Minh",
                    IgnoreHTTPSErrors = false,
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    Args = ["--disable-blink-features=AutomationControlled", "--disable-dev-shm-usage", "--no-sandbox"]
                });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                                             || ex.Message.Contains("chromium", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Chưa cài Chromium cho Playwright. Chạy lệnh: playwright install chromium", ex);
        }
    }

    private async Task<IPage> EnsurePageAsync(UserSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (session.Page is { IsClosed: false })
        {
            return session.Page;
        }

        var existingPage = session.Context.Pages.FirstOrDefault(p => !p.IsClosed);
        if (existingPage != null)
        {
            session.Page = existingPage;
            return existingPage;
        }

        var page = await session.Context.NewPageAsync();
        page.SetDefaultTimeout(_playwrightSettings.Timeout);
        page.SetDefaultNavigationTimeout(_playwrightSettings.Timeout * 2);
        session.Page = page;
        return page;
    }

    private async Task EnsureLoggedInAsync(
        IPage page,
        string serverUrl,
        string username,
        string password,
        string? authHost,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(page.Url) || page.Url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            await page.GotoAsync(serverUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = _playwrightSettings.Timeout
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (IsAuthPage(page.Url, authHost))
        {
            await FillLoginFormAsync(page, username, password);
            return;
        }

        var isCurrentUser = await IsCurrentUserAsync(page, username);
        if (isCurrentUser)
        {
            return;
        }

        await TryLogoutAsync(page);
        await page.GotoAsync(serverUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = _playwrightSettings.Timeout
        });

        if (IsAuthPage(page.Url, authHost))
        {
            await FillLoginFormAsync(page, username, password);
            return;
        }

        if (await IsCurrentUserAsync(page, username))
        {
            return;
        }

        if (!IsAuthPage(page.Url, authHost))
        {
            throw new InvalidOperationException("Không điều hướng được về trang đăng nhập authen.mplis.gov.vn.");
        }
    }

    private async Task EnsureSearchPageAsync(IPage page, string searchPageUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (page.Url.Contains("/CungCapThongTinGiayChungNhan/Index", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await page.GotoAsync(searchPageUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = _playwrightSettings.Timeout
        });
    }

    private async Task<IPlaywright> EnsurePlaywrightAsync(CancellationToken cancellationToken)
    {
        if (_playwright != null)
        {
            return _playwright;
        }

        await _playwrightInitLock.WaitAsync(cancellationToken);
        try
        {
            if (_playwright != null)
            {
                return _playwright;
            }

            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            return _playwright;
        }
        finally
        {
            _playwrightInitLock.Release();
        }
    }

    private async Task FillLoginFormAsync(IPage page, string username, string password)
    {
        await page.FillAsync("input[name='username']", username);
        await page.FillAsync("input[name='password']", password);
        await page.ClickAsync("button[type='submit'].login100-form-btn");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        if (page.Url.Contains("authen.mplis.gov.vn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Đăng nhập thất bại. Vui lòng kiểm tra tài khoản hoặc mật khẩu.");
        }

        await page.WaitForSelectorAsync("a.user-profile b", new PageWaitForSelectorOptions
        {
            Timeout = _playwrightSettings.Timeout
        });
    }

    private async Task<bool> IsCurrentUserAsync(IPage page, string username)
    {
        try
        {
            await page.WaitForSelectorAsync("a.user-profile b", new PageWaitForSelectorOptions
            {
                Timeout = Math.Min(_playwrightSettings.Timeout, 10000)
            });

            var loggedInUsername = await page.InnerTextAsync("a.user-profile b");
            return loggedInUsername.Trim().Equals(username.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task TryLogoutAsync(IPage page)
    {
        try
        {
            await page.ClickAsync("a.user-profile");
            await Task.Delay(350);
            await page.ClickAsync("a[href*='/Account/Logout']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Logout skipped because current page has no logout control.");
        }
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var idleMinutes = _playwrightSettings.SessionIdleMinutes > 0 ? _playwrightSettings.SessionIdleMinutes : 30;
        var maxIdle = TimeSpan.FromMinutes(idleMinutes);

        foreach (var entry in _sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var session = entry.Value;
            if (now - session.LastAccessUtc < maxIdle)
            {
                continue;
            }

            if (!await session.Gate.WaitAsync(0, cancellationToken))
            {
                continue;
            }

            var removed = false;
            try
            {
                if (now - session.LastAccessUtc < maxIdle)
                {
                    continue;
                }

                if (_sessions.TryRemove(entry.Key, out var removedSession))
                {
                    await DisposeSessionAsync(removedSession);
                    removed = true;
                    _logger.LogInformation("Disposed idle session key={SessionKey}", entry.Key);
                }
            }
            finally
            {
                session.Gate.Release();
                if (removed)
                {
                    session.Gate.Dispose();
                }
            }
        }
    }

    private static async Task<string> PostAdvancedSearchAsync(IPage page, string url, string payload)
    {
        const string script = """
                              async ([targetUrl, formPayload]) => {
                                  if (typeof $ === 'undefined' || typeof $.ajax === 'undefined') {
                                      return JSON.stringify({ statusText: 'error', error: 'jQuery not available on page' });
                                  }

                                  return await new Promise((resolve) => {
                                      $.ajax({
                                          url: targetUrl,
                                          type: 'POST',
                                          data: formPayload,
                                          contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
                                          timeout: 120000,
                                          success: function(data) {
                                              resolve(typeof data === 'object' ? JSON.stringify(data) : data);
                                          },
                                          error: function(xhr, status, error) {
                                              resolve(JSON.stringify({
                                                  statusText: status,
                                                  error: error,
                                                  status: xhr.status,
                                                  responseText: xhr.responseText
                                              }));
                                          }
                                      });
                                  });
                              }
                              """;

        return await page.EvaluateAsync<string>(script, new object[] { url, payload.Replace("\n", "").Replace("\r", "") });
    }

    private static List<VbdlisSummaryRecord> ExtractSummary(JsonObject root)
    {
        if (root["data"] is not JsonArray dataArray)
        {
            return [];
        }

        var results = new List<VbdlisSummaryRecord>();

        foreach (var dataNode in dataArray)
        {
            if (dataNode is not JsonObject dataObj)
            {
                continue;
            }

            var gcn = dataObj["GiayChungNhan"] as JsonObject;
            var owners = ExtractOwners(dataObj["ChuSoHuu"] as JsonArray);
            var thuaDat = ExtractThuaDat(dataObj);
            var taiSan = ExtractTaiSan(dataObj["TaiSan"] as JsonArray);

            var giayChungNhanId = GetString(gcn, "Id") ?? GetString(gcn, "giayChungNhanId");
            results.Add(new VbdlisSummaryRecord
            {
                GiayChungNhanId = giayChungNhanId,
                SoPhatHanh = GetString(gcn, "soPhatHanh"),
                SoVaoSo = GetString(gcn, "soVaoSo"),
                NgayVaoSo = GetString(gcn, "ngayVaoSo"),
                ChuSoHuu = owners,
                ThuaDat = thuaDat,
                TaiSan = taiSan
            });
        }

        return results;
    }

    private static List<VbdlisCompactRecord> BuildCompactData(IEnumerable<VbdlisSummaryRecord> summaryData)
    {
        return summaryData
            .Select(record => new VbdlisCompactRecord
            {
                GiayChungNhanId = record.GiayChungNhanId,
                ChuSuDungCompact = BuildChuSuDungCompact(record.ChuSoHuu),
                GiayChungNhanCompact = BuildGiayChungNhanCompact(record.SoPhatHanh, record.SoVaoSo, record.NgayVaoSo),
                ThuaDatCompact = BuildThuaDatCompact(record.ThuaDat),
                TaiSanCompact = BuildTaiSanCompact(record.TaiSan)
            })
            .ToList();
    }

    private static List<VbdlisOwnerSummary> ExtractOwners(JsonArray? ownersArray)
    {
        if (ownersArray == null)
        {
            return [];
        }

        var owners = new List<VbdlisOwnerSummary>();
        foreach (var ownerNode in ownersArray)
        {
            if (ownerNode is not JsonObject ownerObj)
            {
                continue;
            }

            owners.Add(new VbdlisOwnerSummary
            {
                HoTen = GetString(ownerObj, "hoTen"),
                SoGiayTo = GetString(ownerObj, "soGiayTo"),
                DiaChi = GetString(ownerObj, "diaChi")
            });
        }

        return owners;
    }

    private static List<VbdlisThuaDatSummary> ExtractThuaDat(JsonObject dataObj)
    {
        var dedup = new Dictionary<string, VbdlisThuaDatSummary>(StringComparer.OrdinalIgnoreCase);

        if (dataObj["TaiSan"] is JsonArray taiSanArray)
        {
            foreach (var taiSanNode in taiSanArray)
            {
                if (taiSanNode is not JsonObject taiSanObj)
                {
                    continue;
                }

                var soTo = GetString(taiSanObj, "soHieuToBanDo");
                var soThua = GetString(taiSanObj, "soThuTuThua");
                var diaChi = GetString(taiSanObj, "diaChi");
                AddThuaDat(dedup, soTo, soThua, diaChi, null, null);
            }
        }

        var gcn = dataObj["GiayChungNhan"] as JsonObject;
        if (gcn?["ListDangKyQuyen"] is JsonArray listDangKyQuyen)
        {
            foreach (var dkqNode in listDangKyQuyen)
            {
                if (dkqNode is not JsonObject dkqObj)
                {
                    continue;
                }

                var tdObj = dkqObj["ThuaDat"] as JsonObject;
                if (tdObj == null)
                {
                    continue;
                }

                var soTo = GetString(tdObj, "soHieuToBanDo");
                var soThua = GetString(tdObj, "soThuTuThua");
                var diaChi = GetString(tdObj, "diaChi");
                var dienTich = GetString(tdObj, "dienTich");
                var mucDich = GetString(tdObj, "mucDichSuDungGhep") ?? GetString(tdObj, "maThua");
                AddThuaDat(dedup, soTo, soThua, diaChi, dienTich, mucDich);
            }
        }

        return dedup.Values.ToList();
    }

    private static List<VbdlisTaiSanSummary> ExtractTaiSan(JsonArray? taiSanArray)
    {
        if (taiSanArray == null)
        {
            return [];
        }

        var dedup = new Dictionary<string, VbdlisTaiSanSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var taiSanNode in taiSanArray)
        {
            if (taiSanNode is not JsonObject taiSanObj)
            {
                continue;
            }

            var soTo = GetString(taiSanObj, "soHieuToBanDo");
            var soThua = GetString(taiSanObj, "soThuTuThua");
            var diaChi = GetString(taiSanObj, "diaChi");
            var soHieuCanHo = GetString(taiSanObj, "soHieuCanHo");
            var tenTaiSan = GetString(taiSanObj, "tenTaiSan");
            var dienTichXayDung = GetString(taiSanObj, "dienTichXayDung");
            var dienTichSuDung = GetString(taiSanObj, "dienTichSuDung");
            var soTang = GetString(taiSanObj, "soTang");

            var key = $"{tenTaiSan}|{soTo}|{soThua}|{diaChi}|{soHieuCanHo}|{dienTichXayDung}|{dienTichSuDung}|{soTang}";
            dedup.TryAdd(key, new VbdlisTaiSanSummary
            {
                TenTaiSan = tenTaiSan,
                SoTo = soTo,
                SoThua = soThua,
                DiaChi = diaChi,
                SoHieuCanHo = soHieuCanHo,
                DienTichXayDung = dienTichXayDung,
                DienTichSuDung = dienTichSuDung,
                SoTang = soTang
            });
        }

        return dedup.Values.ToList();
    }

    private static void AddThuaDat(
        Dictionary<string, VbdlisThuaDatSummary> dedup,
        string? soTo,
        string? soThua,
        string? diaChi,
        string? dienTich,
        string? mucDichSuDung)
    {
        if (string.IsNullOrWhiteSpace(soTo) &&
            string.IsNullOrWhiteSpace(soThua) &&
            string.IsNullOrWhiteSpace(diaChi) &&
            string.IsNullOrWhiteSpace(dienTich) &&
            string.IsNullOrWhiteSpace(mucDichSuDung))
        {
            return;
        }

        var key = $"{soTo}|{soThua}|{diaChi}|{dienTich}|{mucDichSuDung}";
        dedup.TryAdd(key, new VbdlisThuaDatSummary
        {
            SoTo = soTo,
            SoThua = soThua,
            DiaChi = diaChi,
            DienTich = dienTich,
            MucDichSuDung = mucDichSuDung
        });
    }

    private static string BuildChuSuDungCompact(IEnumerable<VbdlisOwnerSummary> owners)
    {
        var sections = owners
            .Select(owner =>
            {
                var lines = new List<string>();
                if (!string.IsNullOrWhiteSpace(owner.HoTen))
                {
                    lines.Add($"Họ tên: {owner.HoTen}");
                }

                if (!string.IsNullOrWhiteSpace(owner.SoGiayTo))
                {
                    lines.Add($"Số giấy tờ: {owner.SoGiayTo}");
                }

                if (!string.IsNullOrWhiteSpace(owner.DiaChi))
                {
                    lines.Add($"Địa chỉ: {owner.DiaChi}");
                }

                return string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join($"{Environment.NewLine}---{Environment.NewLine}", sections);
    }

    private static string BuildGiayChungNhanCompact(string? soPhatHanh, string? soVaoSo, string? ngayVaoSo)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(soPhatHanh))
        {
            lines.Add($"Số phát hành: {soPhatHanh}");
        }

        if (!string.IsNullOrWhiteSpace(soVaoSo))
        {
            lines.Add($"Số vào sổ: {soVaoSo}");
        }

        var ngayVaoSoDisplay = FormatNgayVaoSo(ngayVaoSo);
        if (!string.IsNullOrWhiteSpace(ngayVaoSoDisplay))
        {
            lines.Add($"Ngày vào sổ: {ngayVaoSoDisplay}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildThuaDatCompact(IEnumerable<VbdlisThuaDatSummary> thuaDatList)
    {
        var sections = thuaDatList
            .Select(thuaDat =>
            {
                var lines = new List<string>();

                if (!string.IsNullOrWhiteSpace(thuaDat.SoTo))
                {
                    lines.Add($"Tờ bản đồ số: {thuaDat.SoTo}");
                }

                if (!string.IsNullOrWhiteSpace(thuaDat.SoThua))
                {
                    lines.Add($"Thửa đất số: {thuaDat.SoThua}");
                }

                if (!string.IsNullOrWhiteSpace(thuaDat.DienTich))
                {
                    lines.Add($"Diện tích: {thuaDat.DienTich} m²");
                }

                if (!string.IsNullOrWhiteSpace(thuaDat.MucDichSuDung))
                {
                    lines.Add($"Mục đích sử dụng: {thuaDat.MucDichSuDung}");
                }

                if (!string.IsNullOrWhiteSpace(thuaDat.DiaChi))
                {
                    lines.Add($"Địa chỉ: {thuaDat.DiaChi}");
                }

                return string.Join(Environment.NewLine, lines);
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join($"{Environment.NewLine}---{Environment.NewLine}", sections);
    }

    private static string BuildTaiSanCompact(IEnumerable<VbdlisTaiSanSummary> taiSanList)
    {
        var sections = taiSanList
            .Select(taiSan =>
            {
                var lines = new List<string>();

                if (!string.IsNullOrWhiteSpace(taiSan.TenTaiSan))
                {
                    lines.Add($"Tên tài sản: {taiSan.TenTaiSan}");
                }

                if (!string.IsNullOrWhiteSpace(taiSan.DienTichXayDung))
                {
                    lines.Add($"Diện tích xây dựng: {taiSan.DienTichXayDung} m²");
                }

                if (!string.IsNullOrWhiteSpace(taiSan.DienTichSuDung))
                {
                    lines.Add($"Diện tích sử dụng: {taiSan.DienTichSuDung} m²");
                }

                if (!string.IsNullOrWhiteSpace(taiSan.SoTang))
                {
                    lines.Add($"Số tầng: {taiSan.SoTang}");
                }

                if (!string.IsNullOrWhiteSpace(taiSan.SoHieuCanHo))
                {
                    lines.Add($"Số hiệu căn hộ: {taiSan.SoHieuCanHo}");
                }

                if (!string.IsNullOrWhiteSpace(taiSan.SoTo))
                {
                    lines.Add($"Tờ bản đồ số: {taiSan.SoTo}");
                }

                if (!string.IsNullOrWhiteSpace(taiSan.SoThua))
                {
                    lines.Add($"Thửa đất số: {taiSan.SoThua}");
                }

                if (!string.IsNullOrWhiteSpace(taiSan.DiaChi))
                {
                    lines.Add($"Địa chỉ: {taiSan.DiaChi}");
                }

                return string.Join(Environment.NewLine, lines);
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join($"{Environment.NewLine}---{Environment.NewLine}", sections);
    }

    private static string? FormatNgayVaoSo(string? ngayVaoSoRaw)
    {
        if (string.IsNullOrWhiteSpace(ngayVaoSoRaw))
        {
            return null;
        }

        if (ngayVaoSoRaw.StartsWith("/Date(", StringComparison.OrdinalIgnoreCase) &&
            ngayVaoSoRaw.EndsWith(")/", StringComparison.OrdinalIgnoreCase))
        {
            var ticksText = ngayVaoSoRaw.Replace("/Date(", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(")/", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (long.TryParse(ticksText, out var ticks))
            {
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(ticks).DateTime;
                if (dt >= new DateTime(1900, 1, 1))
                {
                    return dt.ToString("dd/MM/yyyy");
                }
            }
        }

        if (DateTime.TryParse(ngayVaoSoRaw, out var parsed) && parsed >= new DateTime(1900, 1, 1))
        {
            return parsed.ToString("dd/MM/yyyy");
        }

        return null;
    }

    private static string? GetString(JsonObject? obj, string propertyName)
    {
        var node = obj?[propertyName];
        if (node == null)
        {
            return null;
        }

        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<int>(out var intValue) => intValue.ToString(),
            JsonValue value when value.TryGetValue<long>(out var longValue) => longValue.ToString(),
            JsonValue value when value.TryGetValue<decimal>(out var decimalValue) => decimalValue.ToString(),
            _ => node.ToJsonString()
        };
    }

    private static int? GetNullableInt(JsonObject? obj, string propertyName)
    {
        var node = obj?[propertyName];
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (value.TryGetValue<long>(out var longValue))
        {
            return longValue is <= int.MaxValue and >= int.MinValue ? (int)longValue : null;
        }

        if (value.TryGetValue<string>(out var textValue) && int.TryParse(textValue, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private string ResolveServerUrl(string? server)
    {
        var selected = string.IsNullOrWhiteSpace(server) ? _vbdlisSettings.BaseUrl : server.Trim();
        if (!Uri.TryCreate(selected, UriKind.Absolute, out _))
        {
            throw new ArgumentException($"Server không hợp lệ: {selected}", nameof(server));
        }

        return selected;
    }

    private string BuildSearchPageUrl(string serverUrl)
    {
        var dcBase = ResolveDcBaseUrl(serverUrl);
        return CombineUrl(dcBase, _vbdlisSettings.CungCapThongTinGCN);
    }

    private string BuildAdvancedSearchUrl(string serverUrl)
    {
        var dcBase = ResolveDcBaseUrl(serverUrl);
        return CombineUrl(dcBase, "CungCapThongTinGiayChungNhanAjax/AdvancedSearchGiayChungNhan");
    }

    private static string CombineUrl(string baseUrl, string relativePath)
    {
        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private string ResolveDcBaseUrl(string serverUrl)
    {
        var uri = new Uri(serverUrl);
        var root = $"{uri.Scheme}://{uri.Host}";
        if (!uri.IsDefaultPort)
        {
            root += $":{uri.Port}";
        }

        if (uri.AbsolutePath.Contains("/dc", StringComparison.OrdinalIgnoreCase))
        {
            return CombineUrl(root, "dc");
        }

        if (Uri.TryCreate(_vbdlisSettings.BaseUrl, UriKind.Absolute, out var configuredBase) &&
            configuredBase.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) &&
            configuredBase.AbsolutePath.Contains("/dc", StringComparison.OrdinalIgnoreCase))
        {
            return CombineUrl(root, "dc");
        }

        return CombineUrl(root, "dc");
    }

    private static bool IsAuthPage(string currentUrl, string? authHost)
    {
        if (string.IsNullOrWhiteSpace(currentUrl))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(authHost))
        {
            return currentUrl.Contains(authHost, StringComparison.OrdinalIgnoreCase);
        }

        return currentUrl.Contains("authen.mplis.gov.vn", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private string BuildUserDataDir(string serverUrl, string username)
    {
        var uri = new Uri(serverUrl);
        var host = uri.Host.Replace('.', '_');
        var user = SanitizeForPath(username.Trim().ToLowerInvariant());
        var root = string.IsNullOrWhiteSpace(_playwrightSettings.UserDataRoot)
            ? Path.Combine(AppContext.BaseDirectory, "playwright-user-data")
            : _playwrightSettings.UserDataRoot;

        return Path.Combine(root, $"{host}_{user}");
    }

    private static string BuildSessionKey(string serverUrl, string username)
    {
        var uri = new Uri(serverUrl);
        return $"{uri.Host}|{username.Trim().ToLowerInvariant()}";
    }

    private static string SanitizeForPath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "user";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var buffer = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            buffer[i] = invalidChars.Contains(input[i]) ? '_' : input[i];
        }

        return new string(buffer);
    }

    private static string BuildAdvancedSearchPayload(string soGiayTo, int tinhId)
    {
        if (tinhId <= 0)
        {
            tinhId = 24;
        }

        var formData = new StringBuilder();
        formData.Append("draw=2&");
        formData.Append("columns%5B0%5D%5Bdata%5D=&");
        formData.Append("columns%5B0%5D%5Bname%5D=&");
        formData.Append("columns%5B0%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B0%5D%5Borderable%5D=false&");
        formData.Append("columns%5B0%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B0%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B1%5D%5Bdata%5D=GiayChungNhan&");
        formData.Append("columns%5B1%5D%5Bname%5D=GiayChungNhan&");
        formData.Append("columns%5B1%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B1%5D%5Borderable%5D=false&");
        formData.Append("columns%5B1%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B1%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B2%5D%5Bdata%5D=ChuSoHuu&");
        formData.Append("columns%5B2%5D%5Bname%5D=ChuSoHuu&");
        formData.Append("columns%5B2%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B2%5D%5Borderable%5D=false&");
        formData.Append("columns%5B2%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B2%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("columns%5B3%5D%5Bdata%5D=TaiSan&");
        formData.Append("columns%5B3%5D%5Bname%5D=TaiSan&");
        formData.Append("columns%5B3%5D%5Bsearchable%5D=true&");
        formData.Append("columns%5B3%5D%5Borderable%5D=false&");
        formData.Append("columns%5B3%5D%5Bsearch%5D%5Bvalue%5D=&");
        formData.Append("columns%5B3%5D%5Bsearch%5D%5Bregex%5D=false&");
        formData.Append("start=0&");
        formData.Append("length=10&");
        formData.Append("search%5Bvalue%5D=&");
        formData.Append("search%5Bregex%5D=false&");
        formData.Append("isAdvancedSearch=true&");
        formData.Append($"tinhId={tinhId}&");
        formData.Append("xaId=0&");
        formData.Append("huyenId=0&");
        formData.Append("timChinhXac=true&");
        formData.Append("andOperator=false&");
        formData.Append("loaiGiayChungNhanId=&");
        formData.Append("maVach=&");
        formData.Append("soPhatHanh=&");
        formData.Append("soVaoSo=&");
        formData.Append("soHoSoGoc=&");
        formData.Append("soHoSoGocCu=&");
        formData.Append("soVaoSoCu=&");
        formData.Append("hoTen=&");
        formData.Append("namSinh=&");
        formData.Append($"soGiayTo={Uri.EscapeDataString(soGiayTo)}&");
        formData.Append("soThuTuThua=&");
        formData.Append("soHieuToBanDo=&");
        formData.Append("soThuTuThuaCu=&");
        formData.Append("soHieuToBanDoCu=&");
        formData.Append("soNha=&");
        formData.Append("diaChiChiTiet=");
        return formData.ToString();
    }

    private async Task DisposeSessionAsync(UserSession session)
    {
        try
        {
            session.Page = null;
            await session.Context.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed disposing session key={SessionKey}", session.Key);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VbdlisPlaywrightSearchService));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var entry in _sessions.ToArray())
        {
            if (_sessions.TryRemove(entry.Key, out var session))
            {
                await DisposeSessionAsync(session);
                session.Gate.Dispose();
            }
        }

        _playwrightInitLock.Dispose();
        _sessionCreateLock.Dispose();

        _playwright?.Dispose();
        _playwright = null;
    }

    private sealed class UserSession(
        string key,
        string serverUrl,
        string username,
        bool headless,
        string userDataDir,
        IBrowserContext context,
        SemaphoreSlim gate,
        DateTimeOffset lastAccessUtc)
    {
        public string Key { get; } = key;
        public string ServerUrl { get; } = serverUrl;
        public string Username { get; } = username;
        public bool Headless { get; } = headless;
        public string UserDataDir { get; } = userDataDir;
        public IBrowserContext Context { get; } = context;
        public SemaphoreSlim Gate { get; } = gate;
        public DateTimeOffset LastAccessUtc { get; set; } = lastAccessUtc;
        public IPage? Page { get; set; }
    }
}
