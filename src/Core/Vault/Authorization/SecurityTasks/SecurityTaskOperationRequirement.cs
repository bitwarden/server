using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.Vault.Authorization.SecurityTasks;

public class SecurityTaskOperationRequirement : OperationAuthorizationRequirement
{
    public SecurityTaskOperationRequirement(string name)
    {
        Name = name;
    }
}

public static class SecurityTaskOperations
{
    public static readonly SecurityTaskOperationRequirement Read = new SecurityTaskOperationRequirement(nameof(Read));
    public static readonly SecurityTaskOperationRequirement Create = new SecurityTaskOperationRequirement(nameof(Create));
    public static readonly SecurityTaskOperationRequirement Update = new SecurityTaskOperationRequirement(nameof(Update));

    /// <summary>
    /// List all security tasks for a specific organization.
    /// <example><code>
    /// var orgContext = _currentContext.GetOrganization(organizationId);
    /// _authorizationService.AuthorizeOrThrowAsync(User, SecurityTaskOperations.ListAllForOrganization, orgContext);
    /// </code></example>
    /// </summary>
    public static readonly SecurityTaskOperationRequirement ListAllForOrganization = new SecurityTaskOperationRequirement(nameof(ListAllForOrganization));
}
