#nullable enable

using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

/// <summary>
/// A requirement that implements this interface will be handled by <see cref="OrganizationRequirementHandler"/>,
/// which calls AuthorizeAsync with the organization details from the route.
/// This is used for simple role-based checks.
/// This may only be used on endpoints with {orgId} in their path.
/// </summary>
public interface IOrganizationRequirement : IAuthorizationRequirement
{
    // TODO: avoid injecting all of ICurrentContext?
    public Task<bool> AuthorizeAsync(Guid organizationId, CurrentContextOrganization? organizationClaims, ICurrentContext currentContext);
}
