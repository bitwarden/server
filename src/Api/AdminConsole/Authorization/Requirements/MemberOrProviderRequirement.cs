#nullable enable

using Bit.Api.AdminConsole.Context;
using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// Requires that the user is a member of the organization or a provider for the organization.
/// </summary>
public class MemberOrProviderRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(
        Guid organizationId,
        CurrentContextOrganization? organizationClaims,
        IProviderOrganizationContext providerOrganizationContext)
        => organizationClaims is not null || await providerOrganizationContext.ProviderUserForOrgAsync(organizationId);
}
