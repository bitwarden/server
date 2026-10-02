using System.Security.Claims;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
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
/// <remarks>
/// This prevents privilege escalation by ensuring that a user cannot recover the account of
/// another user with a higher role, in either the organization or a provider.
/// </remarks>
public class RecoverAccountAuthorizationHandler(
    IOrganizationContext organizationContext,
    ICurrentContext currentContext,
    IProviderUserRepository providerUserRepository)
    : AuthorizationHandler<RecoverAccountAuthorizationRequirement, OrganizationUser>
{
    public const string FailureReason = "You are not permitted to recover this user's account.";
    public const string ProviderFailureReason = "You are not permitted to recover this Provider member's account.";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        RecoverAccountAuthorizationRequirement requirement,
        OrganizationUser targetOrganizationUser)
    {
        // Step 1: check that the User has permissions with respect to the organization.
        // This may come from their role in the organization or their provider relationship.
        var canRecoverOrganizationMember =
            AuthorizeMember(context.User, targetOrganizationUser) ||
            await AuthorizeProviderAsync(context.User, targetOrganizationUser);

        if (!canRecoverOrganizationMember)
        {
            context.Fail(new AuthorizationFailureReason(this, FailureReason));
            return;
        }

        // Step 2: check that the User has permissions with respect to any provider the target user is a member of.
        // This prevents an organization admin performing privilege escalation into an unrelated provider,
        // and prevents a provider member escalating into a higher role within their own provider.
        var canRecoverProviderMember = await CanRecoverProviderAsync(targetOrganizationUser);
        if (!canRecoverProviderMember)
        {
            context.Fail(new AuthorizationFailureReason(this, ProviderFailureReason));
            return;
        }

        context.Succeed(requirement);
    }

    private async Task<bool> AuthorizeProviderAsync(ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
    {
        return await organizationContext.IsProviderUserForOrganization(currentUser, targetOrganizationUser.OrganizationId);
    }

    private bool AuthorizeMember(ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
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
                or { Type: OrganizationUserType.Custom, Permissions.ManageResetPassword: true }
        };

        return authorized;
    }

    private async Task<bool> CanRecoverProviderAsync(OrganizationUser targetOrganizationUser)
    {
        if (!targetOrganizationUser.UserId.HasValue)
        {
            // If an OrganizationUser is not linked to a User then it can't be linked to a Provider either.
            // This is invalid but does not pose a privilege escalation risk. Return early and let the command
            // handle the invalid input.
            return true;
        }

        var targetUserProviderUsers =
            await providerUserRepository.GetManyByUserAsync(targetOrganizationUser.UserId.Value);

        // The current user must be able to recover the target user in every provider the target belongs to.
        // Note: we do not expect that a user is a member of more than 1 provider, but there is also no guarantee
        // against it; this returns a sequence, so we handle the possibility.
        var authorized = targetUserProviderUsers.All(AuthorizeProviderMember);
        return authorized;
    }

    private bool AuthorizeProviderMember(ProviderUser targetProviderUser)
    {
        // Current user must be a member of the provider with equal or greater permissions than the user
        // account being recovered. Membership alone is not enough: a Service User recovering a Provider admin
        // would escalate into the higher role.
        var authorized = targetProviderUser.Type switch
        {
            ProviderUserType.ProviderAdmin => currentContext.ProviderProviderAdmin(targetProviderUser.ProviderId),
            _ => currentContext.ProviderUser(targetProviderUser.ProviderId)
        };

        return authorized;
    }
}

