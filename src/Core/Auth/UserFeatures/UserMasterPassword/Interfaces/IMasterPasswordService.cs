using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

public interface IMasterPasswordService
{
    /// <summary>
    /// Mutates the user entity in-memory to set the initial master password state.
    /// Does not persist to the database.
    /// </summary>
    /// <remarks>
    /// Requires that <see cref="User.MasterPassword"/> and <see cref="User.Key"/> are both null.
    /// If <paramref name="salt"/> is provided it is assigned to <see cref="User.MasterPasswordSalt"/>;
    /// otherwise the field is left unchanged.
    /// </remarks>
    void SetInitialMasterPassword(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null);

    /// <summary>
    /// Mutates the user entity and persists the result via <see cref="ISetInitialMasterPasswordStateCommand"/>.
    /// </summary>
    Task SetInitialMasterPasswordAsync(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null);

    /// <summary>
    /// Mutates the user entity in-memory to update an existing master password.
    /// Does not persist to the database.
    /// </summary>
    /// <remarks>
    /// Requires that the user already has a master password (<see cref="User.HasMasterPassword()"/>).
    /// Validates that <paramref name="kdf"/> matches the KDF settings already stored on the user —
    /// this method is for changing the password only, not rotating KDF settings.
    /// If <paramref name="salt"/> is provided it is assigned to <see cref="User.MasterPasswordSalt"/>;
    /// otherwise the field is left unchanged.
    /// </remarks>
    void UpdateMasterPassword(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null);

    /// <summary>
    /// Mutates the user entity and persists the result via <see cref="IUpdateMasterPasswordStateCommand"/>.
    /// </summary>
    Task UpdateMasterPasswordAsync(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null);
}
