using Bit.Core.Exceptions;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Queries;

/// <summary>
/// Helper queries for retrieving cipher details belonging to an organization including collection information.
/// </summary>
/// <remarks>It does not perform any internal authorization checks.</remarks>
public interface IOrganizationCiphersQuery
{
    /// <summary>
    /// Returns ciphers belonging to the organization that the user has been assigned to via collections.
    /// </summary>
    /// <exception cref="FeatureUnavailableException"></exception>
    public Task<IEnumerable<CipherDetailsWithCollections>> GetOrganizationCiphersForUser(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns all ciphers belonging to the organization.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <exception cref="FeatureUnavailableException"></exception>
    public Task<IEnumerable<CipherOrganizationDetailsWithCollections>> GetAllOrganizationCiphers(Guid organizationId);

    /// <summary>
    /// Returns ciphers belonging to the organization that are not assigned to any collection.
    /// </summary>
    /// <exception cref="FeatureUnavailableException"></exception>
    Task<IEnumerable<CipherOrganizationDetails>> GetUnassignedOrganizationCiphers(Guid organizationId);

    /// <summary>
    /// Returns ciphers belonging to the organization that are in the specified collections.
    /// </summary>
    /// <remarks>
    /// Note that the <see cref="CipherOrganizationDetailsWithCollections.CollectionIds"/> will include all collections
    /// the cipher belongs to even if it is not in the <paramref name="collectionIds"/> parameter.
    /// </remarks>
    public Task<IEnumerable<CipherOrganizationDetailsWithCollections>> GetOrganizationCiphersByCollectionIds(
        Guid organizationId, IEnumerable<Guid> collectionIds);
}
