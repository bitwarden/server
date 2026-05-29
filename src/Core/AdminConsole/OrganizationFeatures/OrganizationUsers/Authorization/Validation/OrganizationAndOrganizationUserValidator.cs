using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.AdminConsole.Utilities.v2.Shared;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;
using OrganizationNotFound = Bit.Core.AdminConsole.Utilities.v2.Shared.OrganizationNotFound;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.Validation;

public interface IOrganizationAndOrganizationUserValidator
{
    Task<ValidationResult<ValidOrganizationAndOrganizationUser>> ValidateAsync(OrganizationScope organizationId, Guid organizationUserId);
}

public class OrganizationAndOrganizationUserValidator(IOrganizationRepository organizationRepository, IOrganizationUserRepository organizationUserRepository) : IOrganizationAndOrganizationUserValidator
{
    public async Task<ValidationResult<ValidOrganizationAndOrganizationUser>> ValidateAsync(OrganizationScope organizationId, Guid organizationUserId)
    {
        if (organizationId == Guid.Empty)
        {
            return Invalid(ValidOrganizationAndOrganizationUser.Empty, new OrganizationNotFound());
        }

        if (organizationUserId == Guid.Empty)
        {
            return Invalid(ValidOrganizationAndOrganizationUser.Empty, new OrganizationUserNotFound());
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is null)
        {
            return Invalid(ValidOrganizationAndOrganizationUser.Empty, new OrganizationNotFound());
        }

        var organizationUser = await organizationUserRepository.GetByIdAsync(organizationUserId);
        if (organizationUser is null || organization.Id != organizationUser.OrganizationId)
        {
            return Invalid(ValidOrganizationAndOrganizationUser.Empty, new OrganizationUserNotFound());
        }

        return Valid(new ValidOrganizationAndOrganizationUser
        {
            Organization = organization,
            OrganizationUser = organizationUser
        });
    }
}
