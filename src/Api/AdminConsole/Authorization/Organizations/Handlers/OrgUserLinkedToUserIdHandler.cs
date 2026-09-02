using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Requires that the current user has an OrganizationUser record linked to their UserId for the organization in the
/// route. This performs a direct database lookup rather than relying on JWT claims.
/// WARNING: DO NOT EXPAND THIS TO NEW ROUTES - SEE REMARKS BELOW
/// </summary>
/// <remarks>
/// This requirement only supports existing routes that query the database by UserId + OrganizationId to check membership.
/// This is not recommended - check JWT claims for confirmed members using <see cref="IOrganizationRequirement"/>
/// (e.g. <c>[Authorize&lt;MemberRequirement&gt;]</c>) or create a more specific requirement for your situation.
/// However, we have to support this logic on existing routes due to the invalid SSO JIT provisioning bug (PM-34092).
/// This requirement should be deleted when that bug is resolved.
/// </remarks>
public class OrgUserLinkedToUserIdRequirement : IAuthorizationRequirement;

public class OrgUserLinkedToUserIdHandler(
    IHttpContextAccessor httpContextAccessor,
    IOrganizationUserRepository organizationUserRepository,
    IUserService userService)
    : AuthorizationHandler<OrgUserLinkedToUserIdRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrgUserLinkedToUserIdRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("This handler requires an HTTP context.");

        var userId = userService.GetProperUserId(httpContext.User);
        if (userId is null)
        {
            return;
        }

        var orgId = httpContext.GetOrganizationId();
        var orgUser = await organizationUserRepository.GetByOrganizationAsync(orgId, userId.Value);
        if (orgUser is not null)
        {
            context.Succeed(requirement);
        }
    }
}
