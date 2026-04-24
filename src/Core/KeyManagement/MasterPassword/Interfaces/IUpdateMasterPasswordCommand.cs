using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.MasterPassword.Interfaces;

/// <summary>
/// Validates the provided data against the user, applies the updated master password state
/// to the user object in memory, and persists the changes to the database.
/// </summary>
/// <remarks>
/// KDF settings must remain unchanged. Use <see cref="Bit.Core.KeyManagement.Kdf.IChangeKdfCommand"/>
/// to change KDF settings. Use <see cref="IUpdateMasterPasswordQuery"/> for in-memory mutation only (no persistence).
/// </remarks>
public interface IUpdateMasterPasswordCommand
{
    /// <summary>
    /// Validates <paramref name="data"/> against <paramref name="user"/>, mutates the user
    /// with the updated master password state, and persists the result.
    /// </summary>
    /// <param name="user">The user to update the master password for.</param>
    /// <param name="data">The updated master password data to validate and apply.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data is not valid for the user (see <see cref="UpdateMasterPasswordData.ValidateForUser"/>).
    /// </exception>
    Task RunAsync(User user, UpdateMasterPasswordData data);
}
