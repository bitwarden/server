using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public class InviteOrganizationUsersAuthorizationHandler(ICurrentContext currentContext)
    : AuthorizationHandler<InviteOrganizationUserOperationRequirement, InviteOrganizationUsersRequest>
{
    public const string OwnerCanOnlyConfigureAnotherOwnersAccount = "Only an Owner can configure another Owner's account.";
    public const string DoesNotHavePermissionToMangeUsers = "Your account does not have permission to manage users.";
    public const string CustomUsersCannotManageAdminsOrOwners = "Custom users cannot manage Admins or Owners.";
    public const string EnableCustomPermissionsOrganizationMustBeEnterprise =
        "To enable custom permissions the organization must be on an Enterprise plan.";
    public const string CustomUsersOnlyGrantSamePermissions = "Custom users can only grant the same custom permissions that they have.";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        InviteOrganizationUserOperationRequirement requirement,
        InviteOrganizationUsersRequest inviteOrganizationUsersRequest)
    {
        var authorized = requirement switch
        {
            not null when requirement.Name == nameof(InviteOrganizationUserOperations.Invite) =>
                await CanInviteOrganizationUsersAsync(inviteOrganizationUsersRequest,
                    currentContext,
                    context,
                    this),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    public static async Task<bool> CanInviteOrganizationUsersAsync(InviteOrganizationUsersRequest request,
        ICurrentContext currentContext,
        AuthorizationHandlerContext context,
        IAuthorizationHandler handler)
    {
        if (await currentContext.OrganizationOwner(request.Organization.OrganizationId))
        {
            return true;
        }

        foreach (var invite in request.Invites)
        {
            if (invite.Type == OrganizationUserType.Owner)
            {
                context.Fail(new AuthorizationFailureReason(handler, OwnerCanOnlyConfigureAnotherOwnersAccount));
                return false;
            }

            if (await currentContext.OrganizationAdmin(request.Organization.OrganizationId))
            {
                continue;
            }

            if (!await currentContext.ManageUsers(request.Organization.OrganizationId))
            {
                context.Fail(new AuthorizationFailureReason(handler, DoesNotHavePermissionToMangeUsers));
                return false;
            }

            if (invite.Type == OrganizationUserType.Admin)
            {
                context.Fail(new AuthorizationFailureReason(handler, CustomUsersCannotManageAdminsOrOwners));
                return false;
            }

            if (invite.Type == OrganizationUserType.Custom)
            {
                if (!request.Organization.UseCustomPermissions)
                {
                    context.Fail(new AuthorizationFailureReason(handler, EnableCustomPermissionsOrganizationMustBeEnterprise));
                    return false;
                }

                if (invite.Permissions is null)
                {
                    continue;
                }

                if (!await ValidateCustomPermissionsGrantAsync(invite, request.Organization.OrganizationId, currentContext))
                {
                    context.Fail(new AuthorizationFailureReason(handler, CustomUsersOnlyGrantSamePermissions));
                    return false;
                }
            }
        }

        return true;
    }

    public static async Task<bool> ValidateCustomPermissionsGrantAsync(OrganizationUserInvite invite, Guid organizationId, ICurrentContext currentContext)
    {
        if (invite.Permissions.ManageUsers && !await currentContext.ManageUsers(organizationId))
        {
            return false;
        }

        if (invite.Permissions.AccessReports && !await currentContext.AccessReports(organizationId))
        {
            return false;
        }

        if (invite.Permissions.ManageGroups && !await currentContext.ManageGroups(organizationId))
        {
            return false;
        }

        if (invite.Permissions.ManagePolicies && !await currentContext.ManagePolicies(organizationId))
        {
            return false;
        }

        if (invite.Permissions.ManageScim && !await currentContext.ManageScim(organizationId))
        {
            return false;
        }

        if (invite.Permissions.ManageSso && !await currentContext.ManageSso(organizationId))
        {
            return false;
        }

        if (invite.Permissions.AccessEventLogs && !await currentContext.AccessEventLogs(organizationId))
        {
            return false;
        }

        if (invite.Permissions.AccessImportExport && !await currentContext.AccessImportExport(organizationId))
        {
            return false;
        }

        if (invite.Permissions.EditAnyCollection && !await currentContext.EditAnyCollection(organizationId))
        {
            return false;
        }

        if (invite.Permissions.ManageResetPassword && !await currentContext.ManageResetPassword(organizationId))
        {
            return false;
        }

        var org = currentContext.GetOrganization(organizationId);
        if (org == null)
        {
            return false;
        }

        if (invite.Permissions.CreateNewCollections && !org.Permissions.CreateNewCollections)
        {
            return false;
        }

        if (invite.Permissions.DeleteAnyCollection && !org.Permissions.DeleteAnyCollection)
        {
            return false;
        }

        return true;
    }
}

public class InviteOrganizationUserOperationRequirement : OperationAuthorizationRequirement;

public static class InviteOrganizationUserOperations
{
    public static readonly InviteOrganizationUserOperationRequirement Invite = new() { Name = nameof(Invite) };
}
