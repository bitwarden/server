using System.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class CollectionCipherRepository : BaseRepository, ICollectionCipherRepository
{
    public CollectionCipherRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public CollectionCipherRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId, bool useFlexibleCollections)
    {
        var sprocName = useFlexibleCollections
            ? "[dbo].[CollectionCipher_ReadByUserId_V2]"
            : "[dbo].[CollectionCipher_ReadByUserId]";

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionCipher>(
                sprocName,
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

    public async Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId, bool useFlexibleCollections)
    {
        var sprocName = useFlexibleCollections
            ? "[dbo].[CollectionCipher_ReadByUserIdCipherId_V2]"
            : "[dbo].[CollectionCipher_ReadByUserIdCipherId]";

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionCipher>(
                sprocName,
                new { UserId = userId, CipherId = cipherId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds, bool useFlexibleCollections)
    {
        var sprocName = useFlexibleCollections
            ? "[dbo].[CollectionCipher_UpdateCollections_V2]"
            : "[dbo].[CollectionCipher_UpdateCollections]";

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                sprocName,
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
        Guid organizationId, IEnumerable<Guid> collectionIds, bool useFlexibleCollections)
    {
        var sprocName = useFlexibleCollections
            ? "[dbo].[CollectionCipher_UpdateCollectionsForCiphers_V2]"
            : "[dbo].[CollectionCipher_UpdateCollectionsForCiphers]";

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                sprocName,
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
