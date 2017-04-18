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

        public async Task<ICollection<SubvaultUserSubvaultDetails>> GetManyDetailsByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultUserSubvaultDetails>(
                    $"[{Schema}].[SubvaultUserSubvaultDetails_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<SubvaultUserUserDetails>> GetManyDetailsBySubvaultIdAsync(Guid subvaultId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultUserUserDetails>(
                    $"[{Schema}].[SubvaultUserUserDetails_ReadBySubvaultId]",
                    new { SubvaultId = subvaultId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<bool> GetCanEditByUserIdCipherIdAsync(Guid userId, Guid cipherId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<bool>(
                    $"[{Schema}].[SubvaultUser_ReadCanEditByCipherIdUserId]",
                    new { UserId = userId, CipherId = cipherId },
                    commandType: CommandType.StoredProcedure);

                return result;
            }
        }
    }
}
