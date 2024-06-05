using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Auth.Validators;

public class WebauthnKeyRotationValidator : IRotationValidator<IEnumerable<WebauthnRotateCredentialData>, IEnumerable<WebauthnRotateCredentialData>>
{
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;
    private readonly IUserService _userService;

    public WebauthnKeyRotationValidator(IWebAuthnCredentialRepository webAuthnCredentialRepository, IUserService userService)
    {
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
        _userService = userService;
    }

    public async Task<IEnumerable<WebauthnRotateCredentialData>> ValidateAsync(User user, IEnumerable<WebauthnRotateCredentialData> keysToRotate)
    {
        var result = new List<WebauthnRotateCredentialData>();
        var existing = await _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id);
        if (existing == null || !existing.Any())
        {
            return result;
        }
        // exclude keys where prf is not used
        var existingPrf = existing.Where(c => c.EncryptedUserKey != null);

        foreach (var ea in existingPrf)
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

            result.Add(keyToRotate);
        }

        return result;
    }
}
