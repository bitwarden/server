using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Billing.Models.Requirements;

public class ManageOrganizationBillingRequirement : IAuthorizationRequirement;

public class OrganizationBillingAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IOrganizationContext organizationContext,
    IProviderOrganizationRepository providerOrganizationRepository,
    IUserService userService)
    : AuthorizationHandler<ManageOrganizationBillingRequirement>
{
    public const string NoHttpContextError = "This method should only be called in the context of an HTTP Request.";
    public const string NoUserIdError = "This method should only be called on the private api with a logged in user.";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ManageOrganizationBillingRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException(NoHttpContextError);
        }

        var organizationId = httpContext.GetOrganizationId();
        var userId = userService.GetProperUserId(context.User);
        if (userId == null)
        {
            throw new InvalidOperationException(NoUserIdError);
        }

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
