#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

/// <summary>
/// Requires that the user is a member of the organization or a provider for the organization.
/// </summary>
public class MemberOrProviderRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(Guid organizationId, CurrentContextOrganization? organizationClaims, ICurrentContext currentContext)
        => organizationClaims is not null || await currentContext.ProviderUserForOrgAsync(organizationId);
}
