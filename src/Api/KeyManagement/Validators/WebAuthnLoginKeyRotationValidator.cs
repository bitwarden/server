using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Api.KeyManagement.Validators;

/// <summary>
/// Validates WebAuthn credentials during key rotation. Only processes credentials that support PRF
/// and have encrypted user, public, and private keys. Ensures all such credentials are included
/// in the rotation request with the required encrypted keys.
/// </summary>
public class WebAuthnLoginKeyRotationValidator : IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>,
    IEnumerable<WebAuthnLoginRotateKeyData>>
{
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;

    public WebAuthnLoginKeyRotationValidator(IWebAuthnCredentialRepository webAuthnCredentialRepository)
    {
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
    }

    public async Task<IEnumerable<WebAuthnLoginRotateKeyData>> ValidateAsync(User user,
        IEnumerable<WebAuthnLoginRotateKeyRequestModel> keysToRotate)
    {
        var result = new List<WebAuthnLoginRotateKeyData>();
        var validCredentials = (await _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id))
            .Where(credential => credential is
            {
                SupportsPrf: true,
                EncryptedUserKey: not null,
                EncryptedPublicKey: not null,
                EncryptedPrivateKey: not null
            }).ToList();
        if (validCredentials.Count == 0)
        {
            return result;
        }

        foreach (var webAuthnCredential in validCredentials)
        {
            var keyToRotate = keysToRotate.FirstOrDefault(c => c.Id == webAuthnCredential.Id);
            if (keyToRotate == null)
            {
                throw new BadRequestException("All existing webauthn prf keys must be included in the rotation.");
            }

            if (keyToRotate.EncryptedUserKey == null)
            {
                throw new BadRequestException("WebAuthn prf keys must have user-key during rotation.");
            }
            if (keyToRotate.EncryptedPublicKey == null)
            {
                throw new BadRequestException("WebAuthn prf keys must have public-key during rotation.");
            }

            result.Add(keyToRotate.ToWebAuthnRotateKeyData());
        }

        return result;
    }
}
