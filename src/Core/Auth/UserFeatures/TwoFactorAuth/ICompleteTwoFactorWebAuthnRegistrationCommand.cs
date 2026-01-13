using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface ICompleteTwoFactorWebAuthnRegistrationCommand
{
    /// <summary>
    /// Enshrines WebAuthn 2FA credential registration after a successful challenge.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="attestationResponse"></param>
    /// <returns>Whether or not persisting the credential was successful.</returns>
    Task<bool> CompleteTwoFactorWebAuthnRegistrationAsync(User user, int id, string name,
        AuthenticatorAttestationRawResponse attestationResponse);
}
