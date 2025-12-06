using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Models;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Dịch vụ lưu trữ thông tin đăng nhập một cách an toàn sử dụng DPAPI (Windows), Keychain (macOS) hoặc Secret Service (Linux).
    /// </summary>
    public class CredentialService : ICredentialService
    {
        private readonly string _credentialsFilePath;
        private const string ServiceName = "Haihv.Vbdlis.Tools";
        private const string AccountName = "CurrentSession";

        /// <summary>
        /// Khởi tạo một thể hiện mới của lớp <see cref="CredentialService"/>.
        /// Thiết lập đường dẫn lưu trữ tệp tin trên Windows.
        /// </summary>
        public CredentialService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Haihv.Vbdlis.Tools"
            );

            Directory.CreateDirectory(appDataPath);
            _credentialsFilePath = Path.Combine(appDataPath, "credentials.dat");
        }

        /// <summary>
        /// Lưu thông tin đăng nhập một cách an toàn.
        /// </summary>
        /// <param name="credentials">Thông tin phiên đăng nhập cần lưu.</param>
        public async Task SaveCredentialsAsync(LoginSessionInfo credentials)
        {
            var json = JsonSerializer.Serialize(credentials);

            if (OperatingSystem.IsWindows())
            {
                var plainBytes = Encoding.UTF8.GetBytes(json);
                // Use DPAPI to encrypt data for the current user
                byte[] additionalEntropy = Encoding.UTF8.GetBytes("Haihv.Vbdlis.Tools.Entropy");
                var encryptedBytes = ProtectedData.Protect(plainBytes, additionalEntropy, DataProtectionScope.CurrentUser);
                var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
                await File.WriteAllTextAsync(_credentialsFilePath, encryptedBase64);
            }
            else if (OperatingSystem.IsMacOS())
            {
                // Use macOS Keychain via 'security' CLI
                // -a: Account, -s: Service, -w: Password data, -U: Update if exists
                RunCommand("security", "add-generic-password", "-a", AccountName, "-s", ServiceName, "-w", json, "-U");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux Secret Service (GNOME Keyring / KWallet)
                // Cú pháp: secret-tool store --label="{Label}" service {ServiceName} account {AccountName}
                var psi = new ProcessStartInfo("secret-tool")
                {
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("store");
                psi.ArgumentList.Add("--label=Haihv.Vbdlis.Tools Credentials");
                psi.ArgumentList.Add("service");
                psi.ArgumentList.Add(ServiceName);
                psi.ArgumentList.Add("account");
                psi.ArgumentList.Add(AccountName);

                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException("Cannot start secret-tool process.");
                await process.StandardInput.WriteAsync(json);
                process.StandardInput.Close();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    throw new Exception($"secret-tool exited with code {process.ExitCode}: {error}");
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Credential storage is only supported on Windows, macOS, and Linux.");
            }
        }

        /// <summary>
        /// Tải thông tin đăng nhập đã lưu.
        /// </summary>
        /// <returns>Thông tin phiên đăng nhập nếu tìm thấy và giải mã thành công; ngược lại trả về null.</returns>
        public async Task<LoginSessionInfo?> LoadCredentialsAsync()
        {
            try
            {
                string? json = null;

                if (OperatingSystem.IsWindows())
                {
                    if (!File.Exists(_credentialsFilePath)) return null;

                    var encryptedBase64 = await File.ReadAllTextAsync(_credentialsFilePath);
                    if (string.IsNullOrWhiteSpace(encryptedBase64)) return null;

                    var encryptedBytes = Convert.FromBase64String(encryptedBase64);
                    byte[] additionalEntropy = Encoding.UTF8.GetBytes("Haihv.Vbdlis.Tools.Entropy");
                    var plainBytes = ProtectedData.Unprotect(encryptedBytes, additionalEntropy, DataProtectionScope.CurrentUser);
                    json = Encoding.UTF8.GetString(plainBytes);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    // -w: Output password only
                    json = RunCommand("security", "find-generic-password", "-a", AccountName, "-s", ServiceName, "-w");
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Linux Secret Service (GNOME Keyring / KWallet)
                    // Cú pháp: secret-tool lookup service {ServiceName} account {AccountName}
                    // Yêu cầu máy Linux phải cài: libsecret-tools (Ubuntu/Debian)
                    json = RunCommand("secret-tool", "lookup", "service", ServiceName, "account", AccountName);
                }
                else
                {
                    // Thay vì ném lỗi, có thể return null nếu muốn không crash app
                    throw new PlatformNotSupportedException("Credential storage is not supported on this platform.");
                }
                return string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<LoginSessionInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Xóa thông tin đăng nhập đã lưu.
        /// </summary>
        public Task ClearCredentialsAsync()
        {
            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(_credentialsFilePath))
                {
                    File.Delete(_credentialsFilePath);
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                try
                {
                    RunCommand("security", "delete-generic-password", "-a", AccountName, "-s", ServiceName);
                }
                catch
                {
                    Debug.WriteLine("No existing credentials found in Keychain.");
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                try
                {
                    RunCommand("secret-tool", "clear", "service", ServiceName, "account", AccountName);
                }
                catch
                {
                    Debug.WriteLine("No existing credentials found in Secret Service.");
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Chạy một lệnh hệ thống và trả về kết quả đầu ra chuẩn.
        /// </summary>
        /// <param name="fileName">Tên tệp thực thi hoặc lệnh.</param>
        /// <param name="args">Danh sách các tham số truyền vào lệnh.</param>
        /// <returns>Chuỗi kết quả đầu ra từ lệnh (đã loại bỏ ký tự xuống dòng cuối cùng), hoặc null nếu không khởi chạy được.</returns>
        /// <exception cref="Exception">Ném ra ngoại lệ nếu lệnh trả về mã thoát khác 0.</exception>
        private static string? RunCommand(string fileName, params string[] args)
        {
            var psi = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                // Throwing exception for non-zero exit code to handle "not found" cases in caller
                throw new Exception($"Command '{fileName}' failed with exit code {process.ExitCode}: {error}");
            }

            return output.TrimEnd('\n', '\r');
        }
    }
}
