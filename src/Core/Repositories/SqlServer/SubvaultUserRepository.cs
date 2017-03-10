using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Linq;

namespace Bit.Core.Repositories.SqlServer
{
    public class SubvaultUserRepository : Repository<SubvaultUser, Guid>, ISubvaultUserRepository
    {
        public SubvaultUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public SubvaultUserRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<ICollection<SubvaultUser>> GetManyByOrganizationUserIdAsync(Guid orgUserId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultUser>(
                    $"[{Schema}].[{Table}_ReadByOrganizationUserId]",
                    new { OrganizationUserId = orgUserId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
