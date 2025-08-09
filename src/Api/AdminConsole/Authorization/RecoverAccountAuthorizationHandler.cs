using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
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

public class RecoverAccountAuthorizationHandler(IHttpContextAccessor httpContextAccessor,
    IProviderUserRepository providerUserRepository,
    IUserService userService)
    : AuthorizationHandler<RecoverAccountAuthorizationRequirement, OrganizationUser>
{
    public const string NoUserIdError = "This method should only be called on the private api with a logged in user.";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        RecoverAccountAuthorizationRequirement requirement,
        OrganizationUser targetOrganizationUser)
    {
        var httpContext = httpContextAccessor.GetHttpContextOrThrow();

        var authorized =
            await AuthorizeMemberAsync(httpContext, targetOrganizationUser) ||
            await AuthorizeProviderAsync(httpContext, targetOrganizationUser);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> AuthorizeProviderAsync(HttpContext httpContext, OrganizationUser targetOrganizationUser)
    {
        var userId = userService.GetProperUserId(httpContext.User);
        if (userId == null)
        {
            throw new InvalidOperationException(NoUserIdError);
        }

        var isProviderUserForOrganization = await httpContext.IsProviderUserForOrgAsync(providerUserRepository,
            userId.Value, targetOrganizationUser.OrganizationId);

        return isProviderUserForOrganization;
    }

    private async Task<bool> AuthorizeMemberAsync(HttpContext httpContext, OrganizationUser targetOrganizationUser)
    {
        var currentContextOrganization = httpContext.User.GetCurrentContextOrganization(targetOrganizationUser.OrganizationId);
        if (currentContextOrganization == null)
        {
            // The user is not a provider or member of the organization - cannot authorize
            return false;
        }

        var roleAuthorized = targetOrganizationUser.Type switch
        {
            OrganizationUserType.Owner => currentContextOrganization.Type is OrganizationUserType.Owner,
            OrganizationUserType.Admin => currentContextOrganization.Type is OrganizationUserType.Owner or OrganizationUserType.Admin,
            _ => currentContextOrganization is
                { Type: OrganizationUserType.Owner or OrganizationUserType.Admin }
                or { Type: OrganizationUserType.Custom, Permissions.ManageResetPassword: true}
        };

        if (!roleAuthorized)
        {
            return false;
        }

        // Even if the role is authorized, we need to make sure they're not trying to recover a provider's account
        var providerUsers =
            await providerUserRepository.GetManyByOrganizationAsync(targetOrganizationUser.OrganizationId);
        var targetUserIsProvider = providerUsers.Any(pu => pu.UserId == targetOrganizationUser.UserId!.Value);

        return !targetUserIsProvider;
    }
}
