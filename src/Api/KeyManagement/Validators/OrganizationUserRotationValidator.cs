using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Api.KeyManagement.Validators;

/// <summary>
/// Organization user implementation for <see cref="IRotationValidator{T,R}"/>
/// Currently responsible for validation of user reset password keys (used by admins to perform account recovery) during user key rotation
/// </summary>
public class OrganizationUserRotationValidator : IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>,
    IReadOnlyList<OrganizationUser>>
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public OrganizationUserRotationValidator(IOrganizationUserRepository organizationUserRepository) =>
        _organizationUserRepository = organizationUserRepository;

    public async Task<IReadOnlyList<OrganizationUser>> ValidateAsync(User user,
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys)
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
        existing = existing.Where(o => OrganizationUser.IsValidResetPasswordKey(o.ResetPasswordKey)).ToList();

        foreach (var ou in existing)
        {
            var organizationUser = resetPasswordKeys.FirstOrDefault(a => a.OrganizationId == ou.OrganizationId);
            if (organizationUser == null)
            {
                throw new BadRequestException("All existing reset password keys must be included in the rotation.");
            }

            // Should be migrated to: if (!OrganizationUser.IsValidResetPasswordKey(organizationUser.ResetPasswordKey))
            // after https://bitwarden.atlassian.net/browse/PM-31001 is resolved
            if (organizationUser.ResetPasswordKey == null)
            {
                throw new BadRequestException("Reset Password keys cannot be set to null during rotation.");
            }

            ou.ResetPasswordKey = organizationUser.ResetPasswordKey;
            result.Add(ou);
        }

        return result;
    }
}
