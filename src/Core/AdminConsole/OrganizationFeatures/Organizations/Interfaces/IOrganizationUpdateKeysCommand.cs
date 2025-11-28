using Bit.Core.AdminConsole.Entities;

public interface IOrganizationUpdateKeysCommand
{
    /// <summary>
    /// Update the keys for an organization.
    /// </summary>
    /// <param name="orgId">The ID of the organization to update.</param>
    /// <param name="publicKey">The public key for the organization.</param>
    /// <param name="privateKey">The private key for the organization.</param>
    /// <returns>The updated organization.</returns>
    Task<Organization> UpdateOrganizationKeysAsync(Guid orgId, string publicKey, string privateKey);
}
