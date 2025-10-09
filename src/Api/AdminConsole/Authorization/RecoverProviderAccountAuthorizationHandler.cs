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
            // Cannot recover an OrganizationUser that is not linked to a User.
            // This should be checked as part of the command validation, but it's also required
            // for this logic to work properly, so we'll fail here if not set.
            context.Fail();
        }

        var targetUserProviderUsers =
            await providerUserRepository.GetManyByUserAsync(targetOrganizationUser.UserId!.Value);

        if (targetUserProviderUsers.Any(providerUser => !currentContext.ProviderUser(providerUser.ProviderId)))
        {
            var failureReason = new AuthorizationFailureReason(this, "You cannot recover a provider user account.");
            context.Fail(failureReason);
        }
    }
}
