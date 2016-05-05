using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Domains;
using Bit.Core.Repositories.SqlServer.Models;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class SiteRepository : Repository<Site, SiteTableModel>, ISiteRepository
    {
        public SiteRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Site> GetByIdAsync(string id, string userId)
        {
            var site = await GetByIdAsync(id);
            if(site == null || site.UserId != userId)
            {
                return null;
            }

            return site;
        }

        public async Task<ICollection<Site>> GetManyByUserIdAsync(string userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SiteTableModel>(
                    $"[{Schema}].[{Table}_ReadByUserId]",
                    new { UserId = new Guid(userId) },
                    commandType: CommandType.StoredProcedure);

                return results.Select(s => s.ToDomain()).ToList();
            }
        }

        public async Task<ICollection<Site>> GetManyByRevisionDateAsync(string userId, DateTime sinceRevisionDate)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SiteTableModel>(
                    $"[{Schema}].[{Table}_ReadByRevisionDate]",
                    new { UserId = new Guid(userId), SinceRevisionDate = sinceRevisionDate },
                    commandType: CommandType.StoredProcedure);

                return results.Select(f => f.ToDomain()).ToList();
            }
        }
    }
}
