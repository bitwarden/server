using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Linq;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

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

        public async Task<ICollection<SubvaultUserDetails>> GetManyDetailsByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultUserDetails>(
                    $"[{Schema}].[SubvaultUserDetails_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<SubvaultUserPermissions>> GetPermissionsByUserIdAsync(Guid userId,
            IEnumerable<Guid> subvaultIds, Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultUserPermissions>(
                    $"[{Schema}].[SubvaultUser_ReadPermissionsBySubvaultUserId]",
                    new { UserId = userId, SubvaultIds = subvaultIds.ToGuidIdArrayTVP(), OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<bool> GetIsAdminByUserIdCipherIdAsync(Guid userId, Guid cipherId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<bool>(
                    $"[{Schema}].[SubvaultUser_ReadIsAdminByCipherIdUserId]",
                    new { UserId = userId, CipherId = cipherId },
                    commandType: CommandType.StoredProcedure);

                return result;
            }
        }
    }
}
