using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface ICompleteTwoFactorWebAuthnRegistrationCommand
{
    /// <summary>
    /// Enshrines WebAuthn 2FA credential registration after a successful challenge.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="id">ID for the Key credential to complete.</param>
    /// <param name="name">Name for the Key credential to complete.</param>
    /// <param name="attestationResponse">Fido2 attestation response.</param>
    /// <returns>Whether persisting the credential was successful.</returns>
    Task<bool> CompleteTwoFactorWebAuthnRegistrationAsync(User user, int id, string name,
        AuthenticatorAttestationRawResponse attestationResponse);
}
