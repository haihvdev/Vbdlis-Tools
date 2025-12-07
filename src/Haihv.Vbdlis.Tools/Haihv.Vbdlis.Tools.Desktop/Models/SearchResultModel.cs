using System;

namespace Haihv.Vbdlis.Tools.Desktop.Models;

public class SearchResultModel
{
    public string SearchQuery { get; set; } = string.Empty;
    public string SearchType { get; set; } = string.Empty;
    public AdvancedSearchGiayChungNhanResponse? Response { get; set; }
    public DateTime SearchTime { get; set; }
    public int ResultCount => Response?.Data?.Count ?? 0;
    public string Status => ResultCount > 0 ? "Thành công" : "Không tìm thấy";
}
