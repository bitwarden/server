using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

public interface IGetWebAuthnLoginCredentialAssertionOptionsCommand
{
    public AssertionOptions GetWebAuthnLoginCredentialAssertionOptions();
}
