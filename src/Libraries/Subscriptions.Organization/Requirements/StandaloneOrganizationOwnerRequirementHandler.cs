using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bit.Subscriptions.Organization.Requirements;

/// <summary>Supplies <see cref="StandaloneOrganizationOwnerRequirement"/> with the route's organization and
/// the caller's membership and provider signals.</summary>
public sealed class StandaloneOrganizationOwnerRequirementHandler(
    IHttpContextAccessor httpContextAccessor,
    IProviderOrganizationRepository providerOrganizationRepository,
    IUserService userService)
    : AuthorizationHandler<StandaloneOrganizationOwnerRequirement>
{
    public const string NoHttpContextError = "This handler should only be called in the context of an HTTP request.";
    public const string NoUserIdError = "This handler should only be called on the private api with a logged in user.";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StandaloneOrganizationOwnerRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(NoHttpContextError);

        var organizationId = httpContext.GetOrganizationId();
        var organizationClaims = httpContext.User.GetCurrentContextOrganization(organizationId);
        var userId = userService.GetProperUserId(httpContext.User)
            ?? throw new InvalidOperationException(NoUserIdError);

        // A provider-managed org is administered through its provider, so none of its users — its owner
        // included — are authorized here. Callers who don't qualify leave the policy unsatisfied (403).
        var organizationManagedByProvider = (await providerOrganizationRepository.GetManyByUserAsync(userId))
            .Any(providerOrganization => providerOrganization.OrganizationId == organizationId);

        if (!organizationManagedByProvider && organizationClaims is { Type: OrganizationUserType.Owner })
        {
            context.Succeed(requirement);
        }
    }
}
