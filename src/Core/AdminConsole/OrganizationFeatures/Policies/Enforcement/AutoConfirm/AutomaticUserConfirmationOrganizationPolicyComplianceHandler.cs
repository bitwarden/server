using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public class AutomaticUserConfirmationOrganizationPolicyComplianceHandler(
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository)
    : IAutomaticUserConfirmationOrganizationPolicyComplianceHandler
{
    public async Task<ValidationResult<AutomaticUserConfirmationOrganizationPolicyComplianceHandlerRequest>>
        IsOrganizationCompliantAsync(AutomaticUserConfirmationOrganizationPolicyComplianceHandlerRequest request)
    {
        var organizationUsers = await organizationUserRepository.GetManyDetailsByOrganizationAsync(request.OrganizationId);

        if (await ValidateUserComplianceWithSingleOrgAsync(request, organizationUsers) is { } singleOrgNonCompliant)
        {
            return Invalid(request, singleOrgNonCompliant);
        }

        if (await ValidateNoProviderUsersAsync(organizationUsers) is { } orgHasProviderMember)
        {
            return Invalid(request, orgHasProviderMember);
        }

        return Valid(request);
    }

    private async Task<Error?> ValidateUserComplianceWithSingleOrgAsync(
        AutomaticUserConfirmationOrganizationPolicyComplianceHandlerRequest request,
        ICollection<OrganizationUserUserDetails> organizationUsers)
    {
        var userIds = organizationUsers
            .Where(u => u.UserId is not null &&
                        u.Status is OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed or OrganizationUserStatusType.Revoked)
            .Select(u => u.UserId!.Value);

        var hasNonCompliantUser = (await organizationUserRepository.GetManyByManyUsersAsync(userIds))
            .Any(uo => uo.OrganizationId != request.OrganizationId
                       && uo.Status is OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed or OrganizationUserStatusType.Revoked);

        return hasNonCompliantUser ? new UserNotCompliantWithSingleOrganization() : null;
    }

    private async Task<Error?> ValidateNoProviderUsersAsync(ICollection<OrganizationUserUserDetails> organizationUsers)
    {
        var userIds = organizationUsers.Where(x => x.UserId is not null)
            .Select(x => x.UserId!.Value);

        return (await providerUserRepository.GetManyByManyUsersAsync(userIds)).Count != 0
            ? new ProviderExistsInOrganization()
            : null;
    }
}
