using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.MasterPassword.Interfaces;

/// <summary>
/// Validates the provided data against the user and applies the initial master password state
/// to the user object in memory. Does not persist to the database.
/// </summary>
/// <remarks>
/// Use <see cref="ISetInitialMasterPasswordCommand"/> to also persist the changes.
/// </remarks>
public interface ISetInitialMasterPasswordQuery
{
    /// <summary>
    /// Validates <paramref name="data"/> against <paramref name="user"/> and mutates the user
    /// in memory with the initial master password state.
    /// </summary>
    /// <param name="user">The user to apply the initial master password to.</param>
    /// <param name="data">The initial master password data to validate and apply.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data is not valid for the user (see <see cref="SetInitialMasterPasswordData.ValidateForUser"/>).
    /// </exception>
    Task RunAsync(User user, SetInitialMasterPasswordData data);
}
