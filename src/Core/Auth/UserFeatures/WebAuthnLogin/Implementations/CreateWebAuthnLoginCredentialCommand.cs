using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Fido2NetLib;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

public class CreateWebAuthnLoginCredentialCommand : ICreateWebAuthnLoginCredentialCommand
{
    public const int MaxCredentialsPerUser = 5;

    private readonly IFido2 _fido2;
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;
    private readonly ILogger<CreateWebAuthnLoginCredentialCommand> _logger;

    public CreateWebAuthnLoginCredentialCommand(IFido2 fido2,
                                IWebAuthnCredentialRepository webAuthnCredentialRepository,
                                ILogger<CreateWebAuthnLoginCredentialCommand> logger)
    {
        _fido2 = fido2;
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
        _logger = logger;
    }

    public async Task<bool> CreateWebAuthnLoginCredentialAsync(User user, string name, CredentialCreateOptions options, AuthenticatorAttestationRawResponse attestationResponse, bool supportsPrf, string encryptedUserKey = null, string encryptedPublicKey = null, string encryptedPrivateKey = null)
    {
        var existingCredentials = await _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id);
        if (existingCredentials.Count >= MaxCredentialsPerUser)
        {
            return false;
        }

        var existingCredentialIds = existingCredentials.Select(c => c.CredentialId);
        IsCredentialIdUniqueToUserAsyncDelegate callback = (args, cancellationToken) => Task.FromResult(!existingCredentialIds.Contains(CoreHelpers.Base64UrlEncode(args.CredentialId)));

        Fido2.CredentialMakeResult credentialResponse = null;
        try
        {
            credentialResponse = await _fido2.MakeNewCredentialAsync(attestationResponse, options, callback);
        }
        catch (Fido2VerificationException e)
        {
            _logger.LogError(e, "Unable to verify WebAuthn credential.");
            return false;
        }

        var credential = new WebAuthnCredential
        {
            Name = name,
            CredentialId = CoreHelpers.Base64UrlEncode(credentialResponse.Result.CredentialId),
            PublicKey = CoreHelpers.Base64UrlEncode(credentialResponse.Result.PublicKey),
            Type = credentialResponse.Result.CredType,
            AaGuid = credentialResponse.Result.Aaguid,
            Counter = (int)credentialResponse.Result.Counter,
            UserId = user.Id,
            SupportsPrf = supportsPrf,
            EncryptedUserKey = encryptedUserKey,
            EncryptedPublicKey = encryptedPublicKey,
            EncryptedPrivateKey = encryptedPrivateKey
        };

        await _webAuthnCredentialRepository.CreateAsync(credential);
        return true;
    }
}
