using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.MasterPassword.Interfaces;

/// <summary>
/// Validates the provided data against the user, applies the initial master password state
/// to the user object in memory, and persists the changes to the database.
/// </summary>
/// <remarks>
/// Use <see cref="ISetInitialMasterPasswordQuery"/> for in-memory mutation only (no persistence).
/// </remarks>
public interface ISetInitialMasterPasswordCommand
{
    /// <summary>
    /// Validates <paramref name="data"/> against <paramref name="user"/>, mutates the user
    /// with the initial master password state, and persists the result.
    /// </summary>
    /// <param name="user">The user to set the initial master password for.</param>
    /// <param name="data">The initial master password data to validate and apply.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data is not valid for the user (see <see cref="SetInitialMasterPasswordData.ValidateForUser"/>).
    /// </exception>
    Task RunAsync(User user, SetInitialMasterPasswordData data);
}
