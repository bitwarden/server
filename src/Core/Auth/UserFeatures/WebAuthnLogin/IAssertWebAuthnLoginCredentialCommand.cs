using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

public interface IAssertWebAuthnLoginCredentialCommand
{
    public Task<(User, WebAuthnCredential)> AssertWebAuthnLoginCredential(
        AssertionOptions options,
        AuthenticatorAssertionRawResponse assertionResponse
    );
}
