namespace Haihv.Vbdlis.Tools.Desktop.Models
{
    /// <summary>
    /// Thông tin phiên đăng nhập VBDLIS
    /// </summary>
    public record LoginSessionInfo(
        string Server,
        string Username,
        string Password,
        bool HeadlessBrowser = true);
}
