using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Api.KeyManagement.Validators;

/// <summary>
/// Organization user implementation for <see cref="IRotationValidator{T,R}"/>
/// Currently responsible for validation of account recovery keys (used by admins to perform account recovery) during user key rotation
/// </summary>
public class OrganizationUserRotationValidator : IRotationValidator<OrganizationAccountRecoveryRotationData,
    IReadOnlyList<OrganizationUser>>
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public OrganizationUserRotationValidator(IOrganizationUserRepository organizationUserRepository) =>
        _organizationUserRepository = organizationUserRepository;

    public async Task<IReadOnlyList<OrganizationUser>> ValidateAsync(User user,
        OrganizationAccountRecoveryRotationData data)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var result = new List<OrganizationUser>();

        var existing = await _organizationUserRepository.GetManyByUserAsync(user.Id);
        if (existing == null || existing.Count == 0)
        {
            return result;
        }

        // Exclude any account recovery that do not have a key.
        var enrolled = existing.Where(o => o.IsEnrolledInAccountRecovery()).ToList();

        // A V1 to V2 upgrade cannot re-encapsulate the account recovery key, because that needs the organization's
        // public key to be trusted and the upgrade shows the member no prompt.
        var isUpgradeRotation = data.HasV2UpgradeToken && !user.HasV2KeyShape();

        foreach (var organizationUser in enrolled)
        {
            var accountRecovery = data.AccountRecoveryUnlockData
                .FirstOrDefault(a => a.OrganizationId == organizationUser.OrganizationId);
            if (accountRecovery == null)
            {
                throw new BadRequestException("All existing account recovery keys must be included in the rotation.");
            }

            if (isUpgradeRotation)
            {
                if (OrganizationUser.IsValidResetPasswordKey(accountRecovery.ResetPasswordKey))
                {
                    throw new BadRequestException(
                        "Account recovery keys cannot be rotated during a V1 to V2 upgrade rotation.");
                }

                // Keep the stored key. Nothing downstream guards against a null, so overwriting it here would lock
                // the organization out of account recovery for this member.
                result.Add(organizationUser);
                continue;
            }

            if (!OrganizationUser.IsValidResetPasswordKey(accountRecovery.ResetPasswordKey))
            {
                throw new BadRequestException("Account recovery keys cannot be null or empty during rotation.");
            }

            organizationUser.ResetPasswordKey = accountRecovery.ResetPasswordKey;
            result.Add(organizationUser);
        }

        return result;
    }
}
