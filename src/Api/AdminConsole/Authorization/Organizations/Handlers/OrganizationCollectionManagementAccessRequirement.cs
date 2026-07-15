#nullable enable

using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Requires that the user is allowed to create collections, has Can Manage permissions for at least one
/// collection in the organization, or has a custom permission (ManageUsers, ManageGroups, or AccessReports) that
/// depends on this same basic organization member/group information for an unrelated reason (e.g. the Members
/// page, Groups page, and Member Access Report). This data is not privileged - it contains as little information
/// as possible and no cryptographic keys or other sensitive data - but if an organization has restricted
/// collection management to a subset of users, there's no reason to expose it more broadly than the users who
/// actually have some legitimate need for it.
/// </summary>
/// <remarks>
/// This intentionally does not implement <see cref="IOrganizationRequirement"/> because it needs more than JWT
/// claims and a provider check to answer the question - it needs the Organization's ability settings and, in the
/// less common case where collection creation is restricted, a database call to check the user's collection
/// access. Pulls the organization ID from the route itself, following the same shape as
/// <see cref="OrgUserLinkedToUserIdHandler"/>.
/// </remarks>
public class OrganizationCollectionManagementAccessRequirement : IAuthorizationRequirement;

public class OrganizationCollectionManagementAccessHandler(
    IHttpContextAccessor httpContextAccessor,
    IUserService userService,
    IProviderUserRepository providerUserRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    ICollectionRepository collectionRepository)
    : AuthorizationHandler<OrganizationCollectionManagementAccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationCollectionManagementAccessRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("This handler requires an HTTP context.");

        var userId = userService.GetProperUserId(httpContext.User);
        if (userId is null)
        {
            return;
        }

        var orgId = httpContext.GetOrganizationId();
        var organizationClaims = httpContext.User.GetCurrentContextOrganization(orgId);
        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId);

        if (CollectionPermissions.CanCreate(organizationClaims, organizationAbility))
        {
            context.Succeed(requirement);
            return;
        }

        // Custom users who manage org members/groups or view reports have their own legitimate need for this
        // basic directory data, independent of their collection permissions.
        if (organizationClaims is { Type: OrganizationUserType.Custom } &&
            (organizationClaims.Permissions.ManageUsers ||
             organizationClaims.Permissions.ManageGroups ||
             organizationClaims.Permissions.AccessReports))
        {
            context.Succeed(requirement);
            return;
        }

        // The user is a confirmed member of the organization but cannot create collections - check whether they
        // have Can Manage permissions on at least one collection instead.
        if (organizationClaims is not null)
        {
            var collections = await collectionRepository.GetManySharedByOrganizationIdWithPermissionsAsync(
                orgId, userId.Value, includeAccessRelationships: false);
            if (collections.Any(c => c.Manage))
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Allow provider users to access this information if they are a provider for the target organization
        if (await httpContext.IsProviderUserForOrgAsync(providerUserRepository, userId.Value, orgId))
        {
            context.Succeed(requirement);
        }
    }
}
