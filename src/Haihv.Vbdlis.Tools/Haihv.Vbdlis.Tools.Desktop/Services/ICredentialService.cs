using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Services
{
    /// <summary>
    /// Service for storing and retrieving login credentials
    /// </summary>
    public interface ICredentialService
    {
        /// <summary>
        /// Saves login credentials
        /// </summary>
        Task SaveCredentialsAsync(string server, string username, string password, bool headlessBrowser);

        /// <summary>
        /// Loads saved credentials
        /// </summary>
        Task<(string server, string username, string password, bool headlessBrowser)?> LoadCredentialsAsync();

        /// <summary>
        /// Clears saved credentials
        /// </summary>
        Task ClearCredentialsAsync();

        /// <summary>
        /// Checks if credentials exist
        /// </summary>
        Task<bool> HasSavedCredentialsAsync();
    }
}
