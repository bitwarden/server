using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <inheritdoc />
public class OrganizationUserValidationService(IProviderUserRepository providerUserRepository)
    : IOrganizationUserValidationService
{
    public async Task<Error?> CanManage(Guid actingUserId, OrganizationUser? actingUser, OrganizationUser targetUser)
    {
        var authorizedByRole = actingUser switch
        {
            { Type: OrganizationUserType.Owner } => true,
            { Type: OrganizationUserType.Admin } => targetUser.Type is not OrganizationUserType.Owner,
            { Type: OrganizationUserType.Custom } when actingUser.GetPermissions()?.ManageUsers is true =>
                targetUser.Type is OrganizationUserType.User or OrganizationUserType.Custom,
            _ => false
        };

        if (authorizedByRole)
        {
            return null;
        }

        // Provider users aren't org members but hold Owner-level authority. Mirrors CurrentContext.ProviderUserForOrgAsync.
        var actingUserIsProvider = (await providerUserRepository
                .GetManyOrganizationDetailsByUserAsync(actingUserId, ProviderUserStatusType.Confirmed))
            .Any(po => po.OrganizationId == targetUser.OrganizationId);

        return actingUserIsProvider ? null : new CannotManageTargetUser();
    }
}
