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

        public async Task<ICollection<CollectionUserUserDetails>> GetManyDetailsByCollectionIdAsync(Guid organizationId,
            Guid collectionId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<CollectionUserUserDetails>(
                    $"[{Schema}].[CollectionUserUserDetails_ReadByCollectionId]",
                    new { OrganizationId = organizationId, CollectionId = collectionId },
                    commandType: CommandType.StoredProcedure);

                // Return distinct Id results. If at least one of the grouped results is not ReadOnly, that we return it.
                return results
                    .GroupBy(c => c.Id)
                    .Select(g => g.OrderBy(og => og.ReadOnly).First())
                    .ToList();
            }
        }
    }
}
