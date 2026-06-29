using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <inheritdoc />
public class OrganizationUserValidationService : IOrganizationUserValidationService
{
    public Error? CanManage(OrganizationUser? actingUser, OrganizationUser targetUser, bool actingUserIsProvider)
    {
        var authorizedByRole = actingUser switch
        {
            { Type: OrganizationUserType.Owner } => true,
            { Type: OrganizationUserType.Admin } => targetUser.Type is not OrganizationUserType.Owner,
            { Type: OrganizationUserType.Custom } when actingUser.GetPermissions()?.ManageUsers is true =>
                targetUser.Type is OrganizationUserType.User or OrganizationUserType.Custom,
            _ => false
        };

        // Provider users aren't org members but hold Owner-level authority.
        return authorizedByRole || actingUserIsProvider
            ? null
            : new CannotManageTargetUser();
    }
}
