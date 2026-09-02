using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IStartTwoFactorWebAuthnRegistrationCommand
{
    /// <summary>
    /// Initiates WebAuthn 2FA credential registration and generates a challenge for adding a new security key.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <returns>Options for creating a new WebAuthn 2FA credential</returns>
    /// <exception cref="BadRequestException">Maximum allowed number of credentials already registered.</exception>
    Task<CredentialCreateOptions> StartTwoFactorWebAuthnRegistrationAsync(User user);
}
