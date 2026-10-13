using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.Vault.Authorization.Ciphers;

public class CipherOrganizationOperationRequirement : OperationAuthorizationRequirement
{
    public CipherOrganizationOperationRequirement(string name)
    {
        Name = name;
    }
}

public static class CipherOrganizationOperations
{
    /// <summary>
    /// Read any cipher in the organization, regardless of collection access.
    /// <example><code>
    /// await _authorizationService.AuthorizeOrThrowAsync(User, new OrganizationScope(organizationId),
    ///     CipherOrganizationOperations.ReadAnyAsAdmin);
    /// </code></example>
    /// </summary>
    public static readonly CipherOrganizationOperationRequirement ReadAnyAsAdmin =
        new CipherOrganizationOperationRequirement(nameof(ReadAnyAsAdmin));
}
