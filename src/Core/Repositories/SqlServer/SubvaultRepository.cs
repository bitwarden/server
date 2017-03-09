using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Linq;

namespace Bit.Core.Repositories.SqlServer
{
    public class SubvaultRepository : Repository<Subvault, Guid>, ISubvaultRepository
    {
        public SubvaultRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public SubvaultRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Subvault> GetByIdAdminUserIdAsync(Guid id, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Subvault>(
                    $"[{Schema}].[{Table}_ReadByIdAdminUserId]",
                    new { Id = id, UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.FirstOrDefault();
            }
        }

        public async Task<ICollection<Subvault>> GetManyByOrganizationIdAdminUserIdAsync(Guid organizationId, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Subvault>(
                    $"[{Schema}].[{Table}_ReadByOrganizationIdAdminUserId]",
                    new { OrganizationId = organizationId, UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<Subvault>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Subvault>(
                    $"[{Schema}].[{Table}_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
