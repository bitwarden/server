using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
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
/// another user with a higher role or with provider membership.
/// </remarks>
public class RecoverAccountAuthorizationHandler(
    IOrganizationContext organizationContext,
    ICurrentContext currentContext,
    IProviderUserRepository providerUserRepository,
    IOrganizationUserValidationService organizationUserValidationService)
    : AuthorizationHandler<RecoverAccountAuthorizationRequirement, OrganizationUser>
{
    public const string FailureReason = "You are not permitted to recover this user's account.";
    public const string ProviderFailureReason = "You are not permitted to recover a Provider member's account.";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        RecoverAccountAuthorizationRequirement requirement,
        OrganizationUser targetOrganizationUser)
    {
        // CanManageAsync requires a real acting user id; an authenticated request should always have one, but
        // deny rather than substituting a fabricated id (e.g. Guid.Empty) into a security check if it's ever missing.
        if (currentContext.UserId is not { } actingUserId)
        {
            context.Fail(new AuthorizationFailureReason(this, FailureReason));
            return;
        }

        // Step 1: check that the User has permissions with respect to the organization, using the same
        // Owner > Admin > Custom > User hierarchy (plus provider override) as every other "can manage this user"
        // check, but gated on ManageResetPassword instead of the default ManageUsers.
        var actingOrganization = organizationContext.GetOrganizationClaims(context.User, targetOrganizationUser.OrganizationId);

        var manageError = await organizationUserValidationService.CanManageAsync(
            actingUserId, actingOrganization, targetOrganizationUser,
            customPermissionGate: CustomUserManagePermission.ManageResetPassword);

        if (manageError is not null)
        {
            context.Fail(new AuthorizationFailureReason(this, FailureReason));
            return;
        }

        // Step 2: check that the User has permissions with respect to any provider the target user is a member of.
        // This prevents an organization admin performing privilege escalation into an unrelated provider.
        var canRecoverProviderMember = await CanRecoverProviderAsync(targetOrganizationUser);
        if (!canRecoverProviderMember)
        {
            context.Fail(new AuthorizationFailureReason(this, ProviderFailureReason));
            return;
        }

        context.Succeed(requirement);
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

        // If the target user belongs to any provider that the current user is not a member of,
        // deny the action to prevent privilege escalation from organization to provider.
        // Note: we do not expect that a user is a member of more than 1 provider, but there is also no guarantee
        // against it; this returns a sequence, so we handle the possibility.
        var authorized = targetUserProviderUsers.All(providerUser => currentContext.ProviderUser(providerUser.ProviderId));
        return authorized;
    }
}

