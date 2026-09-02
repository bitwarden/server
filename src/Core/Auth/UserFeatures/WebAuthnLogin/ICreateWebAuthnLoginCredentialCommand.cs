using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

public interface ICreateWebAuthnLoginCredentialCommand
{
    public Task<WebAuthnCredential?> CreateWebAuthnLoginCredentialAsync(User user, string name, CredentialCreateOptions options, AuthenticatorAttestationRawResponse attestationResponse, bool supportsPrf, string? encryptedUserKey = null, string? encryptedPublicKey = null, string? encryptedPrivateKey = null);
}
