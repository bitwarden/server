#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public class OrganizationMemberRequirement : IOrganizationRequirement;

public class OrganizationMemberRequirementHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : OrganizationRequirementHandler<OrganizationMemberRequirement>(currentContext, httpContextAccessor)
{
    protected override Task<bool> Authorize(Guid organizationId, CurrentContextOrganization? organizationClaims)
        => Task.FromResult(organizationClaims is not null);
}
