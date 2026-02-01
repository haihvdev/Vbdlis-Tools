using System;

namespace Haihv.Vbdlis.Tools.Desktop.Models;

public sealed class CacheRefreshOption(string displayName, int? days)
{
    public string DisplayName { get; } = displayName;

    public int? Days { get; } = days;

    public bool IsAlways => !Days.HasValue;

    public TimeSpan GetMaxAge()
    {
        return Days.HasValue ? TimeSpan.FromDays(Days.Value) : TimeSpan.Zero;
    }
}