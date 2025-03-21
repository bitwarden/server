using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

public class OrganizationAuthorizeAttribute<T>
    : AuthorizeAttribute, IAuthorizationRequirementData
    where T : IOrganizationRequirement, new()
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        var requirement = new T();
        yield return requirement;
    }
}
