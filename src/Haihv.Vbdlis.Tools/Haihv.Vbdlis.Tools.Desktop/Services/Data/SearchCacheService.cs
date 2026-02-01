using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using Haihv.Vbdlis.Tools.Desktop.Models.Vbdlis;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ZiggyCreatures.Caching.Fusion;

namespace Haihv.Vbdlis.Tools.Desktop.Services.Data;

public class SearchCacheService(IDatabaseService databaseService)
{
    private readonly ILogger _logger = Log.ForContext<SearchCacheService>();
    private readonly IDatabaseService _databaseService = databaseService;
    private readonly IFusionCache _cache = new FusionCache(new FusionCacheOptions());
    private bool _initialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AdvancedSearchGiayChungNhanResponse?> GetOrSetAsync(
        string searchType,
        string searchKey,
        TimeSpan maxAge,
        Func<Task<AdvancedSearchGiayChungNhanResponse?>> factory,
        bool alwaysRefresh)
    {
        await InitializeAsync();

        if (string.IsNullOrWhiteSpace(searchType) || string.IsNullOrWhiteSpace(searchKey))
        {
            return await factory();
        }

        var trimmedKey = searchKey.Trim();
        if (alwaysRefresh || maxAge <= TimeSpan.Zero)
        {
            var fresh = await factory();
            await SaveToPersistentAsync(searchType, trimmedKey, fresh, maxAge);
            if (maxAge > TimeSpan.Zero)
            {
                await SetInMemoryAsync(searchType, trimmedKey, fresh, maxAge);
            }

            return fresh;
        }

        var cacheKey = BuildCacheKey(searchType, trimmedKey);
        var options = new FusionCacheEntryOptions
        {
            Duration = maxAge
        };

        try
        {
            return await _cache.GetOrSetAsync(cacheKey, async _ =>
            {
                var cached = await TryGetFromPersistentAsync(searchType, trimmedKey, maxAge);
                if (cached != null)
                {
                    return cached;
                }

                var fresh = await factory();
                await SaveToPersistentAsync(searchType, trimmedKey, fresh, maxAge);
                return fresh;
            }, options);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to resolve cache for {SearchType}:{SearchKey}", searchType, trimmedKey);
            return await factory();
        }
    }

    public async Task<DateTime?> GetCachedAtAsync(string searchType, string searchKey)
    {
        await InitializeAsync();
        var trimmedKey = searchKey.Trim();
        if (string.IsNullOrWhiteSpace(searchType) || string.IsNullOrWhiteSpace(trimmedKey))
        {
            return null;
        }

        var dbContext = _databaseService.GetDbContext();
        var entry = await dbContext.SearchCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SearchType == searchType && x.SearchKey == trimmedKey);

        return entry?.CachedAt;
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _databaseService.InitializeDatabaseAsync();
        var dbContext = _databaseService.GetDbContext();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS SearchCacheEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SearchType TEXT NOT NULL,
                SearchKey TEXT NOT NULL,
                ResponseJson TEXT NOT NULL,
                CachedAt TEXT NOT NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_SearchCacheEntries_SearchType_SearchKey
            ON SearchCacheEntries (SearchType, SearchKey);
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_SearchCacheEntries_CachedAt
            ON SearchCacheEntries (CachedAt);
            """);

        _initialized = true;
    }

    private async Task<AdvancedSearchGiayChungNhanResponse?> TryGetFromPersistentAsync(
        string searchType,
        string searchKey,
        TimeSpan maxAge)
    {
        var dbContext = _databaseService.GetDbContext();
        var entry = await dbContext.SearchCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SearchType == searchType && x.SearchKey == searchKey);

        if (entry == null)
        {
            return null;
        }

        var age = DateTime.UtcNow - entry.CachedAt;
        if (age > maxAge)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AdvancedSearchGiayChungNhanResponse>(entry.ResponseJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to deserialize cache entry for {SearchType}:{SearchKey}", searchType, searchKey);
            return null;
        }
    }

    private async Task SaveToPersistentAsync(
        string searchType,
        string searchKey,
        AdvancedSearchGiayChungNhanResponse? response,
        TimeSpan maxAge)
    {
        if (response == null || response.Data.Count == 0)
        {
            return;
        }

        try
        {
            var dbContext = _databaseService.GetDbContext();
            var json = JsonSerializer.Serialize(response, JsonOptions);
            var existing = await dbContext.SearchCacheEntries
                .FirstOrDefaultAsync(x => x.SearchType == searchType && x.SearchKey == searchKey);

            if (existing != null)
            {
                existing.ResponseJson = json;
                existing.CachedAt = DateTime.UtcNow;
                dbContext.SearchCacheEntries.Update(existing);
            }
            else
            {
                await dbContext.SearchCacheEntries.AddAsync(new SearchCacheEntry
                {
                    SearchType = searchType,
                    SearchKey = searchKey,
                    ResponseJson = json,
                    CachedAt = DateTime.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();
            _logger.Debug("Cached search result to SQLite for {SearchType}:{SearchKey}", searchType, searchKey);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save cache entry for {SearchType}:{SearchKey}", searchType, searchKey);
        }
    }

    private async Task SetInMemoryAsync(
        string searchType,
        string searchKey,
        AdvancedSearchGiayChungNhanResponse? response,
        TimeSpan maxAge)
    {
        if (response == null || response.Data.Count == 0)
        {
            return;
        }

        var cacheKey = BuildCacheKey(searchType, searchKey);
        var options = new FusionCacheEntryOptions
        {
            Duration = maxAge
        };

        await _cache.SetAsync(cacheKey, response, options);
    }

    private static string BuildCacheKey(string searchType, string searchKey)
    {
        return $"search:{searchType}:{searchKey}";
    }
}