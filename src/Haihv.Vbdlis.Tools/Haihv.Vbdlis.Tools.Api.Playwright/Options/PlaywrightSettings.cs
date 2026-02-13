namespace Haihv.Vbdlis.Tools.Api.Playwright.Options;

public sealed class PlaywrightSettings
{
    public const string SectionName = "PlaywrightSettings";

    public bool Headless { get; set; } = true;
    public int SlowMo { get; set; }
    public int Timeout { get; set; } = 30000;
    public string? UserDataRoot { get; set; }
    public int SessionIdleMinutes { get; set; } = 30;
}
