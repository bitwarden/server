using System;
using Bit.Core.Domains;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Linq;
using System.Collections.Generic;

namespace Bit.Core.Repositories.SqlServer
{
    public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
    {
        public OrganizationRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public OrganizationRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Organization> GetByIdAsync(Guid id, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Organization>(
                    "[dbo].[Organization_ReadByIdUserId]",
                    new { Id = id, UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<ICollection<Organization>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Organization>(
                    "[dbo].[Organization_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
