using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Dapper;
using System;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class SsoUserRepository : Repository<SsoUser, long>, ISsoUserRepository
    {
        public SsoUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public SsoUserRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task DeleteAsync(Guid userId, Guid? organizationId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[SsoUser_Delete]",
                    new { UserId = userId, OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
