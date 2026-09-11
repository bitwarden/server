using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Subscriptions.Organization.Requirements;

/// <summary>
/// Authorizes only an owner of a standalone organization. An organization managed by a provider (MSP or
/// reseller) is administered through that provider, so none of its users — its owner included — are
/// authorized here; they use the provider-managed billing surface instead.
/// </summary>
public class StandaloneOrganizationOwnerRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg,
        Func<Task<bool>> isOrganizationManagedByProvider)
        => organizationClaims is { Type: OrganizationUserType.Owner }
           && !await isOrganizationManagedByProvider();
}
