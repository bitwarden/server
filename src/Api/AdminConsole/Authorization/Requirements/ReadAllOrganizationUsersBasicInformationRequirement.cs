using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

public class ReadAllOrganizationUsersBasicInformationRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextOrganization organizationClaims,
        Func<Task<bool>> isProviderUserForOrg) =>
            organizationClaims is not null ?
                Task.FromResult(true) :
                isProviderUserForOrg();
}
