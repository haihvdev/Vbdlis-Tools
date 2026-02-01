using System;

namespace Haihv.Vbdlis.Tools.Desktop.Entities;

public class SearchCacheEntry
{
    public long Id { get; set; }

    public string SearchType { get; set; } = string.Empty;

    public string SearchKey { get; set; } = string.Empty;

    public string ResponseJson { get; set; } = string.Empty;

    public DateTime CachedAt { get; set; }
}