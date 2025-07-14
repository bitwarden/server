#nullable enable
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Api.Billing.Models.Requirements;

public class ManageOrganizationBillingRequirement : IOrganizationRequirement
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
