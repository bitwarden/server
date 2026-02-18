using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IDeleteTwoFactorWebAuthnCredentialCommand
{
    /// <summary>
    /// Deletes a single Two-factor WebAuthn credential by ID ("Key{id}").
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="id">ID of the credential to delete ("Key{id}").</param>
    /// <returns>Whether deletion was successful.</returns>
    /// <remarks>Will not delete the last registered credential for a user. To delete the last (or single)
    /// registered credential, use <see cref="IUserService.DisableTwoFactorProviderAsync"/></remarks>
    Task<bool> DeleteTwoFactorWebAuthnCredentialAsync(User user, int id);
}


