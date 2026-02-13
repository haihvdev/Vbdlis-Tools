namespace Haihv.Vbdlis.Tools.Api.Playwright.Options;

public sealed class VbdlisSettings
{
    public const string SectionName = "VbdlisSettings";

    public string BaseUrl { get; set; } = "https://bgi.mplis.gov.vn/dc";
    public string AuthenUrl { get; set; } = "https://authen.mplis.gov.vn/account/login";
    public string CungCapThongTinGCN { get; set; } = "CungCapThongTinGiayChungNhan/Index";
}
