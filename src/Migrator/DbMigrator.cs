using System;
using System.Data.SqlClient;
using System.Reflection;
using DbUp;

namespace Bit.Migrator
{
    public static class DbMigrator
    {
        private static void MigrateMsSqlDatabase(string connectionString, int attempt = 1)
        {
            var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;

            try
            {
                using(var connection = new SqlConnection(masterConnectionString))
                {
                    var command = new SqlCommand(
                        "IF ((SELECT COUNT(1) FROM sys.databases WHERE [name] = 'vault') = 0) " +
                        "CREATE DATABASE [vault];", connection);
                    command.Connection.Open();
                    command.ExecuteNonQuery();
                    command.CommandText = "IF ((SELECT DATABASEPROPERTYEX([name], 'IsAutoClose') " +
                        "FROM sys.databases WHERE [name] = 'vault') = 1) " +
                        "ALTER DATABASE [vault] SET AUTO_CLOSE OFF;";
                    command.ExecuteNonQuery();
                }

                var builder = DeployChanges.To
                    .SqlDatabase(connectionString)
                    .JournalToSqlTable("dbo", "Migration")
                    .WithScriptsAndCodeEmbeddedInAssembly(Assembly.GetExecutingAssembly(),
                        s => s.Contains($".DbScripts.") && !s.Contains(".Archive."))
                    .WithTransaction()
                    .WithExecutionTimeout(new TimeSpan(0, 5, 0))
                    .LogToConsole();

                var upgrader = builder.Build();
                var result = upgrader.PerformUpgrade();
                if(result.Successful)
                {
                    Console.WriteLine("Migration successful.");
                }
                else
                {
                    Console.WriteLine("Migration failed.");
                }
            }
            catch(SqlException e)
            {
                if(e.Message.Contains("Server is in script upgrade mode") && attempt < 10)
                {
                    var nextAttempt = attempt + 1;
                    Console.WriteLine("Database is in script upgrade mode. " +
                        "Trying again (attempt #{0})...", nextAttempt);
                    System.Threading.Thread.Sleep(20000);
                    MigrateMsSqlDatabase(connectionString, nextAttempt);
                    return;
                }

                throw e;
            }
        }

    }
}
