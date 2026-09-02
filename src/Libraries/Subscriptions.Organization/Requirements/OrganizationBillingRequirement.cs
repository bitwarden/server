using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Subscriptions.Organization.Requirements;

/// <summary>Authorizes organization billing access to Owners and confirmed provider users.</summary>
public class OrganizationBillingRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
        => organizationClaims switch
        {
            { Type: OrganizationUserType.Owner } => true,
            _ => await isProviderUserForOrg()
        };
}
