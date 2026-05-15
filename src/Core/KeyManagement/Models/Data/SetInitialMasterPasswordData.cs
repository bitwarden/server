using Bit.Core.Entities;

namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// Data for setting an initial master password on a user account that has no existing master password.
/// See <see cref="UpdateMasterPasswordData"/> for updating an existing master password.
/// </summary>
public class SetInitialMasterPasswordData
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; init; }
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; init; }
    public string? MasterPasswordHint { get; init; }

    /// <summary>
    /// Validates that the provided data is consistent with a user account that has no existing master password.
    /// </summary>
    /// <remarks>
    /// Verifies that the user has no existing master password, user key, or master password salt,
    /// and that the provided salts match the user's current salt (email fallback).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the user already has a master password, user key, or master password salt set,
    /// or when the provided salt does not match the user's current salt.
    /// </exception>
    public void ValidateForUser(User user)
    {
        try
        {
            if (user.MasterPassword != null)
            {
                throw new InvalidOperationException("User already has a master password.");
            }

            if (user.Key != null)
            {
                throw new InvalidOperationException("User already has a user key.");
            }

            if (user.MasterPasswordSalt != null)
            {
                throw new InvalidOperationException("User already has a master password salt.");
            }

            MasterPasswordAuthentication.ValidateSaltUnchangedForUser(user);
            MasterPasswordUnlock.ValidateSaltUnchangedForUser(user);
        }
        catch
        {
            throw new InvalidOperationException("The provided master password data is not valid for this user.");
        }
    }
}
