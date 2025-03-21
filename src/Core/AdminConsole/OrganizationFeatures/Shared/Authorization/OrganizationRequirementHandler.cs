#nullable enable

using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

public abstract class OrganizationRequirementHandler<T>(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<T>
    where T : IAuthorizationRequirement
{
    protected abstract Task<bool> Authorize(Guid organizationId, CurrentContextOrganization? organizationClaims);

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, T requirement)
    {
        var organizationId = httpContextAccessor.GetOrganizationId();
        if (organizationId is null)
        {
            return;
        }

        var organization = currentContext.GetOrganization(organizationId.Value);

        var authorized = await Authorize(organizationId.Value, organization);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }
}
