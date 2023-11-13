using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Validators;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Validators;

public class ResetPasswordRotationValidator : IRotationValidator<IEnumerable<ResetPasswordWithIdRequestModel>,
    IEnumerable<OrganizationUser>>
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public ResetPasswordRotationValidator(IOrganizationUserRepository organizationUserRepository) =>
        _organizationUserRepository = organizationUserRepository;

    public async Task<IEnumerable<OrganizationUser>> ValidateAsync(User user,
        IEnumerable<ResetPasswordWithIdRequestModel> accountRecoveryKeys)
    {
        var result = new List<OrganizationUser>();
        if (accountRecoveryKeys == null || !accountRecoveryKeys.Any())
        {
            return result;
        }

        var existing = await _organizationUserRepository.GetManyByUserAsync(user.Id);
        if (existing == null || !existing.Any())
        {
            return result;
        }

        // Exclude any account recovery that do not have a key.
        existing = existing.Where(o => o.ResetPasswordKey != null).ToList();


        foreach (var ou in existing)
        {
            var accountRecovery = accountRecoveryKeys.FirstOrDefault(a => a.OrganizationId == ou.OrganizationId);
            if (accountRecovery == null)
            {
                throw new BadRequestException("All existing account recovery keys must be included in the rotation.");
            }

            if (accountRecovery.ResetPasswordKey == null)
            {
                throw new BadRequestException("Account recovery keys cannot be set to null during rotation.");
            }

            ou.ResetPasswordKey = accountRecovery.ResetPasswordKey;
            result.Add(ou);
        }

        return result;
    }
}
