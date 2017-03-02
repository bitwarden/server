using System;
using Bit.Core.Domains;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using System.Linq;

namespace Bit.Core.Repositories.SqlServer
{
    public class OrganizationUserRepository : Repository<OrganizationUser, Guid>, IOrganizationUserRepository
    {
        public OrganizationUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public OrganizationUserRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<OrganizationUser> GetByIdAsync(Guid id, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUser>(
                    "[dbo].[OrganizationUser_ReadByIdUserId]",
                    new { Id = id, UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }
    }
}
