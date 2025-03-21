#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public class OrganizationMemberRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(Guid organizationId, CurrentContextOrganization? organizationClaims, ICurrentContext currentContext)
        => Task.FromResult(organizationClaims is not null);
}
