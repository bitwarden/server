// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

public interface ICreateWebAuthnLoginCredentialCommand
{
    public Task<CommandResult<WebAuthnCredential>> CreateWebAuthnLoginCredentialAsync(User user, string name, CredentialCreateOptions options, AuthenticatorAttestationRawResponse attestationResponse, bool supportsPrf, string encryptedUserKey = null, string encryptedPublicKey = null, string encryptedPrivateKey = null);
}
