using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Fido2NetLib;
using Fido2NetLib.Objects;

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

    public async Task<WebAuthnCredential?> CreateWebAuthnLoginCredentialAsync(User user, string name, CredentialCreateOptions options, AuthenticatorAttestationRawResponse attestationResponse, bool supportsPrf, string? encryptedUserKey = null, string? encryptedPublicKey = null, string? encryptedPrivateKey = null)
    {
        var existingCredentials = await _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id);
        if (existingCredentials.Count >= MaxCredentialsPerUser)
        {
            return null;
        }

        var existingCredentialIds = existingCredentials.Select(c => c.CredentialId);
        IsCredentialIdUniqueToUserAsyncDelegate callback = (args, cancellationToken) => Task.FromResult(!existingCredentialIds.Contains(CoreHelpers.Base64UrlEncode(args.CredentialId)));

        RegisteredPublicKeyCredential success;
        try
        {
            success = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = callback
            });
        }
        catch (Fido2VerificationException)
        {
            return null;
        }

        var credential = new WebAuthnCredential
        {
            Name = name,
            CredentialId = CoreHelpers.Base64UrlEncode(success.Id),
            PublicKey = CoreHelpers.Base64UrlEncode(success.PublicKey),
            Type = success.AttestationFormat,
            AaGuid = success.AaGuid,
            Counter = (int)success.SignCount,
            UserId = user.Id,
            SupportsPrf = supportsPrf,
            EncryptedUserKey = encryptedUserKey,
            EncryptedPublicKey = encryptedPublicKey,
            EncryptedPrivateKey = encryptedPrivateKey
        };

        await _webAuthnCredentialRepository.CreateAsync(credential);
        return credential;
    }
}
