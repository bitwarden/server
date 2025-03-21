#nullable enable

using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

public interface IOrganizationRequirement : IAuthorizationRequirement
{
    // TODO: avoid injecting all of ICurrentContext?
    public Task<bool> AuthorizeAsync(Guid organizationId, CurrentContextOrganization? organizationClaims, ICurrentContext currentContext);
}

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
