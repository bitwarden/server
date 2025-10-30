using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Prevents a provider user's account from being recovered unless the current user is also a member of the same providers.
/// This prevents privilege escalation from a client organization to a provider via account recovery.
/// This handler does not positively authorize an action, it only disallows it in this case.
/// </summary>
/// <seealso cref="RecoverMemberAccountAuthorizationHandler"/>
public class RecoverProviderAccountAuthorizationHandler(
    ICurrentContext currentContext,
    IProviderUserRepository providerUserRepository)
    : AuthorizationHandler<RecoverAccountAuthorizationRequirement, OrganizationUser>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        RecoverAccountAuthorizationRequirement requirement,
        OrganizationUser targetOrganizationUser)
    {
        if (!targetOrganizationUser.UserId.HasValue)
        {
            // If an OrganizationUser is not linked to a User then it can't be linked to a Provider either.
            // This is invalid but does not pose a privilege escalation risk. Return early and let the command
            // handle the invalid input.
            return;
        }

        var targetUserProviderUsers =
            await providerUserRepository.GetManyByUserAsync(targetOrganizationUser.UserId.Value);

        // If the target user belongs to any provider that the current user is not a member of,
        // deny the action to prevent privilege escalation from organization to provider.
        if (targetUserProviderUsers.Any(providerUser => !currentContext.ProviderUser(providerUser.ProviderId)))
        {
            context.Fail();
        }
    }
}
