using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class CollectionCipherRepository : BaseRepository, ICollectionCipherRepository
{
    public CollectionCipherRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public CollectionCipherRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionCipher>(
                "[dbo].[CollectionCipher_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<CollectionCipher>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionCipher>(
                "[dbo].[CollectionCipher_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionCipher>(
                "[dbo].[CollectionCipher_ReadByUserIdCipherId]",
                new { UserId = userId, CipherId = cipherId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[CollectionCipher_UpdateCollections]",
                new { CipherId = cipherId, UserId = userId, CollectionIds = collectionIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[CollectionCipher_UpdateCollectionsAdmin]",
                new { CipherId = cipherId, OrganizationId = organizationId, CollectionIds = collectionIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId,
        Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[CollectionCipher_UpdateCollectionsForCiphers]",
                new
                {
                    CipherIds = cipherIds.ToGuidIdArrayTVP(),
                    UserId = userId,
                    OrganizationId = organizationId,
                    CollectionIds = collectionIds.ToGuidIdArrayTVP()
                },
                commandType: CommandType.StoredProcedure);
        }
    }
}
