using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Bit.Core.Repositories.SqlServer
{
    public class GroupRepository : Repository<Group, Guid>, IGroupRepository
    {
        public GroupRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public GroupRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Group>(
                    $"[{Schema}].[Group_ReadByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
