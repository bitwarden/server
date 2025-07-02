using Bit.Core.Repositories;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Queries;

public class OrganizationCiphersQuery : IOrganizationCiphersQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICollectionRepository _collectionRepository;

    public OrganizationCiphersQuery(ICipherRepository cipherRepository, ICollectionCipherRepository collectionCipherRepository, ICollectionRepository collectionRepository)
    {
        _cipherRepository = cipherRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _collectionRepository = collectionRepository;
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
    public Task<IEnumerable<CipherOrganizationDetailsWithCollections>> GetAllOrganizationCiphers(Guid organizationId)
    {
        // single call returns each cipher plus its .CollectionIds
        return _cipherRepository
            .GetManyOrganizationDetailsWithCollectionsByOrganizationIdAsync(organizationId);
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

    public async Task<IEnumerable<CipherOrganizationDetailsWithCollections>>
        GetAllOrganizationCiphersExcludingDefaultUserCollections(Guid orgId)
    {
        var defaultCollIds = (await _collectionRepository
                .GetDefaultCollectionIdsByOrganizationIdAsync(orgId))
            .ToHashSet();

        var all = await _cipherRepository
            .GetManyOrganizationDetailsWithCollectionsByOrganizationIdAsync(orgId);

        return all
            .Where(c =>
                !c.CollectionIds.Any()
                ||
                c.CollectionIds.Any(id => !defaultCollIds.Contains(id))
            )
            .ToList();
    }
}
