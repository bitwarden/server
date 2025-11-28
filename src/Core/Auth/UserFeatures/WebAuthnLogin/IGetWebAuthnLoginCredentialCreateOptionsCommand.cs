using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

/// <summary>
/// Get the options required to create a Passkey for login.
/// </summary>
public interface IGetWebAuthnLoginCredentialCreateOptionsCommand
{
    public Task<CredentialCreateOptions> GetWebAuthnLoginCredentialCreateOptionsAsync(User user);
}
