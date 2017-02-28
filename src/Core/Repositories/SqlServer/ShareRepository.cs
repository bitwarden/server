using System;
using Bit.Core.Domains;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;
using System.Data;
using System.Linq;

namespace Bit.Core.Repositories.SqlServer
{
    public class ShareRepository : Repository<Share, Guid>, IShareRepository
    {
        public ShareRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public ShareRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Share> GetByIdAsync(Guid id, Guid userId)
        {
            var share = await GetByIdAsync(id);
            if(share == null || (share.UserId != userId && share.SharerUserId != userId))
            {
                return null;
            }

            return share;
        }

        public async Task<ICollection<Share>> GetManyByCipherId(Guid cipherId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Share>(
                    $"[{Schema}].[Share_ReadByCipherId]",
                    new { CipherId = cipherId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
