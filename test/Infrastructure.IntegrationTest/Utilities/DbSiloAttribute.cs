using Bit.Core.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

public class DbSiloAttribute : TestCustomizerAttribute
{
    public string DatabaseName { get; }

    public DbSiloAttribute(string databaseName)
    {
        DatabaseName = databaseName;
    }

    public override Task CustomizeAsync(CustomizationContext customizationContext)
    {
        var database = customizationContext.Database;
        if (!database.Enabled || string.IsNullOrEmpty(database.ConnectionString))
        {
            // Nothing to customize
            return Task.CompletedTask;
        }

        if (database.Type == SupportedDatabaseProviders.MySql)
        {
            var connectionStringBuilder = new MySqlConnectionStringBuilder(database.ConnectionString)
            {
                Database = DatabaseName
            };

            database.ConnectionString = connectionStringBuilder.ConnectionString;
        }
        else if(database.Type == SupportedDatabaseProviders.Postgres)
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(database.ConnectionString)
            {
                Database = DatabaseName
            };

            database.ConnectionString = connectionStringBuilder.ConnectionString;
        }
        else if (database.Type == SupportedDatabaseProviders.Sqlite)
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder(database.ConnectionString);

            var existingFileInfo = new FileInfo(connectionStringBuilder.DataSource);

            // Should we require that the existing file actually exists?

            var newFileInfo = new FileInfo(Path.Join(existingFileInfo.DirectoryName, $"{DatabaseName}.{existingFileInfo.Extension}"));

            connectionStringBuilder.DataSource = newFileInfo.FullName;
            database.ConnectionString = connectionStringBuilder.ConnectionString;
        }
        else
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(database.ConnectionString)
            {
                DataSource = DatabaseName
            };
            database.ConnectionString = connectionStringBuilder.ConnectionString;
        }
        return Task.CompletedTask;
    }
}
