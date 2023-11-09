using Bit.Core.Entities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class GetWebAuthnLoginCredentialCreateOptionsCommand : IGetWebAuthnLoginCredentialCreateOptionsCommand
{
    public Task<CredentialCreateOptions> GetWebAuthnLoginCredentialCreateOptionsAsync(User user) => throw new NotImplementedException();
}
