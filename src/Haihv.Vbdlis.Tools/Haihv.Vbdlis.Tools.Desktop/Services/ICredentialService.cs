using System.Threading.Tasks;
using Haihv.Vbdlis.Tools.Desktop.Models;

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
        Task SaveCredentialsAsync(LoginSessionInfo loginSessionInfo);

        /// <summary>
        /// Loads saved credentials
        /// </summary>
        Task<LoginSessionInfo?> LoadCredentialsAsync();

        /// <summary>
        /// Clears saved credentials
        /// </summary>
        Task ClearCredentialsAsync();

    }
}
