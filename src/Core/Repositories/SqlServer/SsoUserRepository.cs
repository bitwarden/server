using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Data.SqlClient;
using System.Data;
using Dapper;

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

        public override async Task DeleteAsync(SsoUser ssoUser)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[SsoUser_Delete]",
                    new { UserId = ssoUser.UserId, OrganizationId = ssoUser.OrganizationId },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
