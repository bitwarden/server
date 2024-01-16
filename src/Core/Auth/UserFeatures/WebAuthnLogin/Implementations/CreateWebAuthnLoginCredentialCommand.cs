using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class CreateWebAuthnLoginCredentialCommand : ICreateWebAuthnLoginCredentialCommand
{
    public const int MaxCredentialsPerUser = 5;

    private readonly IFido2 _fido2;
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;

    public CreateWebAuthnLoginCredentialCommand(IFido2 fido2, IWebAuthnCredentialRepository webAuthnCredentialRepository)
    {
        _fido2 = fido2;
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
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

        var success = await _fido2.MakeNewCredentialAsync(attestationResponse, options, callback);

        var credential = new WebAuthnCredential
        {
            Name = name,
            CredentialId = CoreHelpers.Base64UrlEncode(success.Result.CredentialId),
            PublicKey = CoreHelpers.Base64UrlEncode(success.Result.PublicKey),
            Type = success.Result.CredType,
            AaGuid = success.Result.Aaguid,
            Counter = (int)success.Result.Counter,
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
