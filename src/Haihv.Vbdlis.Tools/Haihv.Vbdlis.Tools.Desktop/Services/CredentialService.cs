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
    /// Service for storing login credentials securely using DPAPI (Windows) or Keychain (macOS)
    /// </summary>
    public class CredentialService : ICredentialService
    {
        private readonly string _credentialsFilePath;
        private const string ServiceName = "Haihv.Vbdlis.Tools";
        private const string AccountName = "CurrentSession";

        public CredentialService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Haihv.Vbdlis.Tools"
            );

            Directory.CreateDirectory(appDataPath);
            _credentialsFilePath = Path.Combine(appDataPath, "credentials.dat");
        }

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
                RunSecurityCommand("add-generic-password", "-a", AccountName, "-s", ServiceName, "-w", json, "-U");
            }
            else
            {
                throw new PlatformNotSupportedException("Credential storage is only supported on Windows and macOS.");
            }
        }

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
                    json = RunSecurityCommand("find-generic-password", "-a", AccountName, "-s", ServiceName, "-w");
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Linux Secret Service (GNOME Keyring / KWallet)
                    // Cú pháp: secret-tool lookup service {ServiceName} account {AccountName}
                    // Yêu cầu máy Linux phải cài: libsecret-tools (Ubuntu/Debian)
                    json = RunSecurityCommand("secret-tool", "lookup", "service", ServiceName, "account", AccountName);
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
                    RunSecurityCommand("delete-generic-password", "-a", AccountName, "-s", ServiceName);
                }
                catch
                {
                    // Ignore if not found
                }
            }

            return Task.CompletedTask;
        }

        private static string? RunSecurityCommand(string command, params string[] args)
        {
            var psi = new ProcessStartInfo("security")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add(command);
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                // Throwing exception for non-zero exit code to handle "not found" cases in caller
                throw new Exception($"Security command failed with exit code {process.ExitCode}");
            }

            return output.TrimEnd('\n', '\r');
        }
    }
}
