using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class MaintenanceRepository : BaseRepository, IMaintenanceRepository
    {
        public MaintenanceRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public MaintenanceRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
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

        public async Task DisableCipherAutoStatsAsync()
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "sp_autostats",
                    new { tblname = "[dbo].[Cipher]", flagc = "OFF" },
                    commandType: CommandType.StoredProcedure);
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

        public async Task DeleteExpiredGrantsAsync()
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[Grant_DeleteExpired]",
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 86400);
            }
        }
    }
}
