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

    /// <summary>
    /// Read aggregate counts of the organization's ciphers by type, without exposing any cipher data. Granted to
    /// everyone who can <see cref="ReadAnyAsAdmin"/>, and additionally to custom users with the
    /// <c>ManagePolicies</c> permission, so they can see the impact of an item type restriction, or the
    /// <c>AccessImportExport</c> or <c>AccessReports</c> permissions, which already allow listing every
    /// organization cipher through the sibling <c>organization-details</c> routes.
    /// </summary>
    public static readonly CipherOrganizationOperationRequirement ReadTypeCountsAsAdmin =
        new CipherOrganizationOperationRequirement(nameof(ReadTypeCountsAsAdmin));
}
