using Bit.Core.Entities;

namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// Data for updating a master password on a user account that already has one.
/// KDF settings must remain unchanged — use <see cref="Bit.Core.KeyManagement.Kdf.IChangeKdfCommand"/> to change KDF.
/// See <see cref="SetInitialMasterPasswordData"/> for setting an initial master password.
/// </summary>
public class UpdateMasterPasswordData
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; init; }
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; init; }
    public string? MasterPasswordHint { get; init; }

    /// <summary>
    /// Validates that the provided data is consistent with the user's current KDF and salt configuration.
    /// </summary>
    /// <remarks>
    /// KDF settings and salt must be unchanged, as this operation only updates the master password.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the KDF settings or salt in the provided data do not match the user's current values.
    /// </exception>
    public void ValidateForUser(User user)
    {
        try
        {
            MasterPasswordAuthentication.ValidateSaltUnchangedForUser(user);
            MasterPasswordAuthentication.Kdf.ValidateUnchangedForUser(user);
            MasterPasswordUnlock.ValidateSaltUnchangedForUser(user);
            MasterPasswordUnlock.Kdf.ValidateUnchangedForUser(user);
        }
        catch
        {
            throw new InvalidOperationException("The provided master password data is not valid for this user.");
        }
    }
}
