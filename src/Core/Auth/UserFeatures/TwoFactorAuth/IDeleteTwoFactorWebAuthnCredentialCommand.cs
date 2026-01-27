using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IDeleteTwoFactorWebAuthnCredentialCommand
{
    /// <summary>
    /// Deletes a Two-factor WebAuthn credential by ID.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="id">ID of the credential to delete.</param>
    /// <returns>Whether deletion was successful.</returns>
    Task<bool> DeleteTwoFactorWebAuthnCredentialAsync(User user, int id);
}


