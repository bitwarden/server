using System.Security.Claims;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// An authorization requirement for recovering an organization member's account.
/// </summary>
/// <remarks>
/// Note: this is different to simply being able to manage account recovery. The user must be recovering
/// a member who has equal or lesser permissions than them.
/// </remarks>
public class RecoverAccountAuthorizationRequirement : IAuthorizationRequirement;

/// <summary>
/// Authorizes members and providers to recover a target OrganizationUser's account.
/// </summary>
public class RecoverMemberAccountAuthorizationHandler(
    IOrganizationContext organizationContext)
    : AuthorizationHandler<RecoverAccountAuthorizationRequirement, OrganizationUser>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        RecoverAccountAuthorizationRequirement requirement,
        OrganizationUser targetOrganizationUser)
    {
        var authorized =
            AuthorizeMemberAsync(context.User, targetOrganizationUser) ||
            await AuthorizeProviderAsync(context.User, targetOrganizationUser);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> AuthorizeProviderAsync(ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
    {
        return await organizationContext.IsProviderUserForOrganization(currentUser, targetOrganizationUser.OrganizationId);
    }

    private bool AuthorizeMemberAsync(ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
    {
        var currentContextOrganization = organizationContext.GetOrganizationClaims(currentUser, targetOrganizationUser.OrganizationId);
        if (currentContextOrganization == null)
        {
            return false;
        }

        // Current user must have equal or greater permissions than the user account being recovered
        var authorized = targetOrganizationUser.Type switch
        {
            OrganizationUserType.Owner => currentContextOrganization.Type is OrganizationUserType.Owner,
            OrganizationUserType.Admin => currentContextOrganization.Type is OrganizationUserType.Owner or OrganizationUserType.Admin,
            _ => currentContextOrganization is
                { Type: OrganizationUserType.Owner or OrganizationUserType.Admin }
                or { Type: OrganizationUserType.Custom, Permissions.ManageResetPassword: true}
        };

        return authorized;
    }
}

/// <summary>
/// Prevents a provider user's account from being recovered unless the current user is also a member of the same providers.
/// This prevents unauthorized access to a provider via account recovery.
/// This handler does not positively authorize an action, it only disallows it in this case.
/// </summary>
public class RecoverProviderAccountAuthorizationHandler(
    ICurrentContext currentContext,
    IProviderUserRepository providerUserRepository)
    : AuthorizationHandler<RecoverAccountAuthorizationRequirement, OrganizationUser>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        RecoverAccountAuthorizationRequirement requirement,
        OrganizationUser targetOrganizationUser)
    {
        // Even if the role is authorized, we need to make sure they're not trying to recover a provider's account
        // (regardless of whether they are a provider for this organization specifically)
        var targetUserProviderUsers =
            await providerUserRepository.GetManyByUserAsync(targetOrganizationUser.UserId!.Value);

        if (targetUserProviderUsers.Any(providerUser => !currentContext.ProviderUser(providerUser.ProviderId)))
        {
            var failureReason = new AuthorizationFailureReason(this, "You cannot recover a provider user account.");
            context.Fail(failureReason);
        }
    }
}
