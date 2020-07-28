using Bit.Core.Models.Table;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class SsoUserRepository : BaseRepository, ISsoUserRepository
    {
        public SsoUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public SsoUserRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public virtual async Task CreateAsync(SsoUser obj)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[dbo].[SsoUser_Create]",
                    obj,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public virtual async Task DeleteAsync(SsoUser obj)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    $"[dbo].[SsoUser_DeleteById]",
                    new { UserId = obj.UserId, OrganizationId = obj.OrganizationId },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
