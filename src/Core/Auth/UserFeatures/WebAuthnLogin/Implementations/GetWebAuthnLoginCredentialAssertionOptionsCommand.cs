using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class GetWebAuthnLoginCredentialAssertionOptionsCommand
    : IGetWebAuthnLoginCredentialAssertionOptionsCommand
{
    private readonly IFido2 _fido2;

    public GetWebAuthnLoginCredentialAssertionOptionsCommand(IFido2 fido2)
    {
        _fido2 = fido2;
    }

    public AssertionOptions GetWebAuthnLoginCredentialAssertionOptions()
    {
        return _fido2.GetAssertionOptions(
            Enumerable.Empty<PublicKeyCredentialDescriptor>(),
            UserVerificationRequirement.Required
        );
    }
}
