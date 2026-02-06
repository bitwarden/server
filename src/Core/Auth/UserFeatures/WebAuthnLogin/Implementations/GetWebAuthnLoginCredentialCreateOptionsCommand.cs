using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class GetWebAuthnLoginCredentialCreateOptionsCommand : IGetWebAuthnLoginCredentialCreateOptionsCommand
{
    private readonly IFido2 _fido2;
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;

    public GetWebAuthnLoginCredentialCreateOptionsCommand(IFido2 fido2, IWebAuthnCredentialRepository webAuthnCredentialRepository)
    {
        _fido2 = fido2;
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
    }

    public async Task<CredentialCreateOptions> GetWebAuthnLoginCredentialCreateOptionsAsync(User user)
    {
        var fidoUser = new Fido2User
        {
            DisplayName = user.Name ?? "",
            Name = user.Email,
            Id = user.Id.ToByteArray(),
        };

        // Get existing keys to exclude
        var existingKeys = await _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id);
        var excludeCredentials = existingKeys
            .Select(k => new PublicKeyCredentialDescriptor(CoreHelpers.Base64UrlDecode(k.CredentialId)))
            .ToList();

        var authenticatorSelection = new AuthenticatorSelection
        {
            AuthenticatorAttachment = null,
            RequireResidentKey = true,
            UserVerification = UserVerificationRequirement.Required
        };

        var extensions = new AuthenticationExtensionsClientInputs { };

        var options = _fido2.RequestNewCredential(fidoUser, excludeCredentials, authenticatorSelection,
            AttestationConveyancePreference.None, extensions);

        return options;
    }
}
