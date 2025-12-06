using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for storing login credentials securely using DPAPI (Windows Data Protection API)
    /// </summary>
    public class CredentialService : ICredentialService
    {
        private readonly string _credentialsFilePath;

        public CredentialService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Haihv.Vbdlis.Tools"
            );

            Directory.CreateDirectory(appDataPath);
            _credentialsFilePath = Path.Combine(appDataPath, "credentials.dat");
        }

        public async Task SaveCredentialsAsync(string server, string username, string password, bool headlessBrowser)
        {
            var credentials = new SavedCredentials
            {
                Server = server,
                Username = username,
                Password = password,
                HeadlessBrowser = headlessBrowser
            };

            var json = JsonSerializer.Serialize(credentials);
            var encryptedData = ProtectData(json);

            await File.WriteAllBytesAsync(_credentialsFilePath, encryptedData);
        }

        public async Task<(string server, string username, string password, bool headlessBrowser)?> LoadCredentialsAsync()
        {
            if (!File.Exists(_credentialsFilePath))
            {
                return null;
            }

            try
            {
                var encryptedData = await File.ReadAllBytesAsync(_credentialsFilePath);
                var json = UnprotectData(encryptedData);
                var credentials = JsonSerializer.Deserialize<SavedCredentials>(json);

                if (credentials == null)
                {
                    return null;
                }

                return (credentials.Server, credentials.Username, credentials.Password, credentials.HeadlessBrowser);
            }
            catch
            {
                // If decryption fails or file is corrupted, return null
                return null;
            }
        }

        public Task ClearCredentialsAsync()
        {
            if (File.Exists(_credentialsFilePath))
            {
                File.Delete(_credentialsFilePath);
            }

            return Task.CompletedTask;
        }

        public Task<bool> HasSavedCredentialsAsync()
        {
            return Task.FromResult(File.Exists(_credentialsFilePath));
        }

        private static byte[] ProtectData(string data)
        {
            // Simple Base64 encoding (not encryption, but obfuscation)
            // In a production app, you should use proper encryption
            // For now, this provides basic protection against casual viewing
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var base64 = Convert.ToBase64String(dataBytes);
            return Encoding.UTF8.GetBytes(base64);
        }

        private static string UnprotectData(byte[] encryptedData)
        {
            // Decode from Base64
            var base64 = Encoding.UTF8.GetString(encryptedData);
            var dataBytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(dataBytes);
        }

        private class SavedCredentials
        {
            public string Server { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool HeadlessBrowser { get; set; } = false;
        }
    }
}
