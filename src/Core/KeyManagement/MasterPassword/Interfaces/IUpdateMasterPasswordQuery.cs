using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.MasterPassword.Interfaces;

/// <summary>
/// Validates the provided data against the user and applies the updated master password state
/// to the user object in memory. Does not persist to the database.
/// </summary>
/// <remarks>
/// KDF settings must remain unchanged. Use <see cref="Bit.Core.KeyManagement.Kdf.IChangeKdfCommand"/>
/// to change KDF settings. Use <see cref="IUpdateMasterPasswordCommand"/> to also persist the changes.
/// </remarks>
public interface IUpdateMasterPasswordQuery
{
    /// <summary>
    /// Validates <paramref name="data"/> against <paramref name="user"/> and mutates the user
    /// in memory with the updated master password state.
    /// </summary>
    /// <param name="user">The user to apply the updated master password to.</param>
    /// <param name="data">The updated master password data to validate and apply.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data is not valid for the user (see <see cref="UpdateMasterPasswordData.ValidateForUser"/>).
    /// </exception>
    Task RunAsync(User user, UpdateMasterPasswordData data);
}
