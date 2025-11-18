using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Billing.Models.Requirements;

public class ManageOrganizationBillingRequirement : IAuthorizationRequirement;

public class OrganizationBillingAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IOrganizationContext organizationContext,
    IProviderOrganizationRepository providerOrganizationRepository)
    : AuthorizationHandler<ManageOrganizationBillingRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ManageOrganizationBillingRequirement requirement)
    {
        var organizationId = httpContextAccessor.GetHttpContextOrThrow().GetOrganizationId();

        // Providers are always authorized to make billing changes.
        if (await organizationContext.IsProviderUserForOrganization(context.User, organizationId))
        {
            context.Succeed(requirement);
            return;
        }

        // Owners are only authorized if the organization is not managed by a provider.
        var organizationClaims = organizationContext.GetOrganizationClaims(context.User, organizationId);
        if (organizationClaims is { Type: OrganizationUserType.Owner } &&
            await providerOrganizationRepository.GetByOrganizationId(organizationId) is null)
        {
            context.Succeed(requirement);
        }
    }
}
