using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Services;

public interface ICipherPermissionsService
{
    /// <summary>
    /// Get the permissions for a single cipher.
    /// </summary>
    /// <param name="cipher">The cipher to get permissions for.</param>
    /// <param name="user">The user to get permissions for.</param>
    /// <returns>The permissions for the cipher.</returns>
    Task<CipherPermissionsResponseData> GetCipherPermissionsAsync(Cipher cipher, User user);

    /// <summary>
    /// Get the permissions for multiple ciphers.
    /// </summary>
    /// <param name="ciphers">The ciphers to get permissions for.</param>
    /// <param name="user">The user to get permissions for.</param>
    /// <returns>A dictionary of cipher IDs and their permissions.</returns>
    Task<IDictionary<Guid, CipherPermissionsResponseData>> GetManyCipherPermissionsAsync(IEnumerable<Cipher> ciphers, User user);
}
