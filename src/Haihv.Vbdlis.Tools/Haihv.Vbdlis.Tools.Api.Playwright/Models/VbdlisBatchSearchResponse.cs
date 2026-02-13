using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Haihv.Vbdlis.Tools.Api.Playwright.Models;

public sealed class VbdlisBatchSearchResponse
{
    [JsonPropertyName("requestedAt")]
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "summary";

    [JsonPropertyName("results")]
    public List<VbdlisSearchResultItem> Results { get; init; } = [];
}

public sealed class VbdlisSearchResultItem
{
    [JsonPropertyName("soGiayTo")]
    public string SoGiayTo { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("recordsTotal")]
    public int? RecordsTotal { get; init; }

    [JsonPropertyName("recordsFiltered")]
    public int? RecordsFiltered { get; init; }

    [JsonPropertyName("fullData")]
    public JsonNode? FullData { get; init; }

    [JsonPropertyName("summaryData")]
    public List<VbdlisSummaryRecord>? SummaryData { get; init; }

    [JsonPropertyName("compactData")]
    public List<VbdlisCompactRecord>? CompactData { get; init; }
}

public sealed class VbdlisSummaryRecord
{
    [JsonPropertyName("giayChungNhanId")]
    public string? GiayChungNhanId { get; init; }

    [JsonPropertyName("soPhatHanh")]
    public string? SoPhatHanh { get; init; }

    [JsonPropertyName("soVaoSo")]
    public string? SoVaoSo { get; init; }

    [JsonPropertyName("ngayVaoSo")]
    public string? NgayVaoSo { get; init; }

    [JsonPropertyName("chuSoHuu")]
    public List<VbdlisOwnerSummary> ChuSoHuu { get; init; } = [];

    [JsonPropertyName("thuaDat")]
    public List<VbdlisThuaDatSummary> ThuaDat { get; init; } = [];

    [JsonPropertyName("taiSan")]
    public List<VbdlisTaiSanSummary> TaiSan { get; init; } = [];
}

public sealed class VbdlisCompactRecord
{
    [JsonPropertyName("giayChungNhanId")]
    public string? GiayChungNhanId { get; init; }

    [JsonPropertyName("chuSuDungCompact")]
    public string ChuSuDungCompact { get; init; } = string.Empty;

    [JsonPropertyName("giayChungNhanCompact")]
    public string GiayChungNhanCompact { get; init; } = string.Empty;

    [JsonPropertyName("thuaDatCompact")]
    public string ThuaDatCompact { get; init; } = string.Empty;

    [JsonPropertyName("taiSanCompact")]
    public string TaiSanCompact { get; init; } = string.Empty;
}

public sealed class VbdlisOwnerSummary
{
    [JsonPropertyName("hoTen")]
    public string? HoTen { get; init; }

    [JsonPropertyName("soGiayTo")]
    public string? SoGiayTo { get; init; }

    [JsonPropertyName("diaChi")]
    public string? DiaChi { get; init; }
}

public sealed class VbdlisThuaDatSummary
{
    [JsonPropertyName("soTo")]
    public string? SoTo { get; init; }

    [JsonPropertyName("soThua")]
    public string? SoThua { get; init; }

    [JsonPropertyName("diaChi")]
    public string? DiaChi { get; init; }

    [JsonPropertyName("dienTich")]
    public string? DienTich { get; init; }

    [JsonPropertyName("mucDichSuDung")]
    public string? MucDichSuDung { get; init; }
}

public sealed class VbdlisTaiSanSummary
{
    [JsonPropertyName("tenTaiSan")]
    public string? TenTaiSan { get; init; }

    [JsonPropertyName("soTo")]
    public string? SoTo { get; init; }

    [JsonPropertyName("soThua")]
    public string? SoThua { get; init; }

    [JsonPropertyName("diaChi")]
    public string? DiaChi { get; init; }

    [JsonPropertyName("soHieuCanHo")]
    public string? SoHieuCanHo { get; init; }

    [JsonPropertyName("dienTichXayDung")]
    public string? DienTichXayDung { get; init; }

    [JsonPropertyName("dienTichSuDung")]
    public string? DienTichSuDung { get; init; }

    [JsonPropertyName("soTang")]
    public string? SoTang { get; init; }
}
