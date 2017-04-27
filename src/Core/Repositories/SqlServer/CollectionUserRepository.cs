using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Linq;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class CollectionUserRepository : Repository<CollectionUser, Guid>, ICollectionUserRepository
    {
        public CollectionUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public CollectionUserRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<ICollection<CollectionUser>> GetManyByOrganizationUserIdAsync(Guid orgUserId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<CollectionUser>(
                    $"[{Schema}].[{Table}_ReadByOrganizationUserId]",
                    new { OrganizationUserId = orgUserId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<CollectionUserCollectionDetails>> GetManyDetailsByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<CollectionUserCollectionDetails>(
                    $"[{Schema}].[CollectionUserCollectionDetails_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<CollectionUserUserDetails>> GetManyDetailsByCollectionIdAsync(Guid collectionId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<CollectionUserUserDetails>(
                    $"[{Schema}].[CollectionUserUserDetails_ReadByCollectionId]",
                    new { CollectionId = collectionId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<bool> GetCanEditByUserIdCipherIdAsync(Guid userId, Guid cipherId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<bool>(
                    $"[{Schema}].[CollectionUser_ReadCanEditByCipherIdUserId]",
                    new { UserId = userId, CipherId = cipherId },
                    commandType: CommandType.StoredProcedure);

                return result;
            }
        }
    }
}
