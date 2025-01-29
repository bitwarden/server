using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Queries;

public interface IGetCipherPermissionsForUserQuery
{
    /// <summary>
    /// Retrieves the permissions of every organization cipher (including unassigned) for the
    /// ICurrentContext's user.
    ///
    /// It considers the Collection Management setting for allowing Admin/Owners access to all ciphers.
    /// </summary>
    /// <remarks>
    /// The primary use case of this query is internal cipher authorization logic.
    /// </remarks>
    /// <param name="organizationId"></param>
    /// <returns>A dictionary of CipherIds and a corresponding OrganizationCipherPermission</returns>
    public Task<IDictionary<Guid, OrganizationCipherPermission>> GetByOrganization(Guid organizationId);
}
