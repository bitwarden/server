using Bit.Api.Auth.Models.Request.Webauthn;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Auth.Validators;

public class WebauthnKeyRotationValidator : IRotationValidator<IEnumerable<WebauthnRotateKeyRequestModel>, IEnumerable<WebauthnRotateKeyData>>
{
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;
    private readonly IUserService _userService;

    public WebauthnKeyRotationValidator(IWebAuthnCredentialRepository webAuthnCredentialRepository, IUserService userService)
    {
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
        _userService = userService;
    }

    public async Task<IEnumerable<WebauthnRotateKeyData>> ValidateAsync(User user, IEnumerable<WebauthnRotateKeyRequestModel> keysToRotate)
    {
        // 2024-06: Remove after 3 releases, for backward compatibility
        if (keysToRotate == null)
        {
            return new List<WebauthnRotateKeyData>();
        }

        var result = new List<WebauthnRotateKeyData>();
        var existing = await _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id);
        if (existing == null || !existing.Any())
        {
            return result;
        }

        foreach (var ea in existing)
        {
            var keyToRotate = keysToRotate.FirstOrDefault(c => c.Id == ea.Id);
            if (keyToRotate == null)
            {
                throw new BadRequestException("All existing webauthn prf keys must be included in the rotation.");
            }

            if (keyToRotate.EncryptedUserKey == null)
            {
                throw new BadRequestException("Webauthn prf keys must have user-key during rotation.");
            }
            if (keyToRotate.EncryptedPublicKey == null)
            {
                throw new BadRequestException("Webauthn prf keys must have public-key during rotation.");
            }

            result.Add(keyToRotate.ToWebauthnRotateKeyData());
        }

        return result;
    }
}
