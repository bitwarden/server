using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class MaintenanceRepository : BaseRepository, IMaintenanceRepository
    {
        public MaintenanceRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public MaintenanceRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task UpdateStatisticsAsync()
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[AzureSQLMaintenance]",
                    new { operation = "statistics", mode = "smart", LogToTable = true },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 86400);
            }
        }

        public async Task RebuildIndexesAsync()
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[AzureSQLMaintenance]",
                    new { operation = "index", mode = "smart", LogToTable = true },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 86400);
            }
        }
    }
}
