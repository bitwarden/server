using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace Bit.Function
{
    public static class DatabaseMaintenance
    {
        [FunctionName("DatabaseMaintenance")]
        public static void Run([TimerTrigger("0 0 * * *")]TimerInfo myTimer, TraceWriter log)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["vault_db"].ConnectionString;
            using(var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // ref: http://bit.ly/2zFNcZo
                var cmd = new SqlCommand("[dbo].[AzureSQLMaintenance]", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                // Options: "all", "index", "statistics"
                cmd.Parameters.Add("@operation", SqlDbType.NVarChar).Value = "all";
                // Options: "smart", "dummy"
                cmd.Parameters.Add("@mode", SqlDbType.NVarChar).Value = "smart";
                // Options: 0, 1
                cmd.Parameters.Add("@LogToTable", SqlDbType.Bit).Value = 1;

                // Asynchronous BeginExecuteNonQuery for this long running sproc to avoid timeouts
                var result = cmd.BeginExecuteNonQuery();
                cmd.EndExecuteNonQuery(result);
            }
        }
    }
}
