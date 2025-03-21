using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

public class OrganizationAuthorizeAttribute(Type requirementType)
    : AuthorizeAttribute, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        var requirement = (IOrganizationRequirement)Activator.CreateInstance(requirementType)!;
        yield return requirement;
    }
}
