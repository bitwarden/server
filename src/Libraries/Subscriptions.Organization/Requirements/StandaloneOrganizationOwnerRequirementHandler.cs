using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bit.Subscriptions.Organization.Requirements;

/// <summary>Supplies <see cref="StandaloneOrganizationOwnerRequirement"/> with the caller's membership in the
/// route's organization and whether that organization is provider-managed.</summary>
public sealed class StandaloneOrganizationOwnerRequirementHandler(
    IHttpContextAccessor httpContextAccessor,
    IProviderOrganizationRepository providerOrganizationRepository)
    : AuthorizationHandler<StandaloneOrganizationOwnerRequirement>
{
    public const string NoHttpContextError = "This handler should only be called in the context of an HTTP request.";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StandaloneOrganizationOwnerRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(NoHttpContextError);

        var organizationId = httpContext.GetOrganizationId();
        var organizationClaims = httpContext.User.GetCurrentContextOrganization(organizationId);

        // A provider-managed org is administered through its provider, so none of its users — its owner
        // included — are authorized here. Callers who don't qualify leave the policy unsatisfied (403).
        var organizationManagedByProvider =
            await providerOrganizationRepository.GetByOrganizationId(organizationId) is not null;

        if (!organizationManagedByProvider && organizationClaims is { Type: OrganizationUserType.Owner })
        {
            context.Succeed(requirement);
        }
    }
}
