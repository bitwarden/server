using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class GetWebAuthnLoginCredentialAssertionOptionsCommand : IGetWebAuthnLoginCredentialAssertionOptionsCommand
{
    private readonly IFido2 _fido2;
    private readonly IWebAuthnChallengeCacheProvider _webAuthnChallengeCache;

    public GetWebAuthnLoginCredentialAssertionOptionsCommand(
        IFido2 fido2,
        IWebAuthnChallengeCacheProvider webAuthnChallengeCache)
    {
        _fido2 = fido2;
        _webAuthnChallengeCache = webAuthnChallengeCache;
    }

    public async Task<AssertionOptions> GetWebAuthnLoginCredentialAssertionOptionsAsync()
    {
        var options = _fido2.GetAssertionOptions(Enumerable.Empty<PublicKeyCredentialDescriptor>(), UserVerificationRequirement.Required);
        await _webAuthnChallengeCache.StoreChallengeAsync(options.Challenge);
        return options;
    }
}
