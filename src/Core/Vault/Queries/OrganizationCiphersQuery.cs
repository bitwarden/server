using Bit.Core.Repositories;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Queries;

public class OrganizationCiphersQuery : IOrganizationCiphersQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;

    public OrganizationCiphersQuery(ICipherRepository cipherRepository, ICollectionCipherRepository collectionCipherRepository)
    {
        _cipherRepository = cipherRepository;
        _collectionCipherRepository = collectionCipherRepository;
    }

    /// <summary>
    /// Returns ciphers belonging to the organization that the user has been assigned to via collections.
    /// </summary>
    public async Task<IEnumerable<CipherDetailsWithCollections>> GetOrganizationCiphersForUser(Guid organizationId, Guid userId)
    {
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, withOrganizations: true);
        var orgCiphers = ciphers.Where(c => c.OrganizationId == organizationId).ToList();
        var orgCipherIds = orgCiphers.Select(c => c.Id);

        var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(organizationId);
        var collectionCiphersGroupDict = collectionCiphers
            .Where(c => orgCipherIds.Contains(c.CipherId))
            .GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

        return orgCiphers.Select(c => new CipherDetailsWithCollections(c, collectionCiphersGroupDict));
    }

    /// <summary>
    /// Returns all ciphers belonging to the organization.
    /// </summary>
    /// <param name="organizationId"></param>
    public async Task<IEnumerable<CipherOrganizationDetailsWithCollections>> GetAllOrganizationCiphers(Guid organizationId)
    {
        var orgCiphers = await _cipherRepository.GetManyOrganizationDetailsByOrganizationIdAsync(organizationId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(organizationId);
        var collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

        return orgCiphers.Select(c => new CipherOrganizationDetailsWithCollections(c, collectionCiphersGroupDict));
    }

    /// <summary>
    /// Returns ciphers belonging to the organization that are not assigned to any collection.
    /// </summary>
    public async Task<IEnumerable<CipherOrganizationDetails>> GetUnassignedOrganizationCiphers(Guid organizationId)
    {
        return await _cipherRepository.GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organizationId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CipherOrganizationDetailsWithCollections>> GetOrganizationCiphersByCollectionIds(
        Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        var managedCollectionIds = collectionIds.ToHashSet();
        var allOrganizationCiphers = await GetAllOrganizationCiphers(organizationId);
        return allOrganizationCiphers.Where(c => c.CollectionIds.Intersect(managedCollectionIds).Any());
    }
}
