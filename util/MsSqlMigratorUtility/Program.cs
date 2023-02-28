using Bit.Migrator;
using CommandDotNet;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

internal class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    [DefaultCommand]
    public void Execute(
        [Option('c', Description = "Connection string to the database on which migrations will be executed.")]
        string databaseConnectionString,
        [Option('v')]
        bool verbose) => MigrateDatabase(databaseConnectionString, verbose);

    private static void MigrateDatabase(string databaseConnectionString, bool verbose = false, int attempt = 1)
    {
        var logger = CreateLogger(verbose);

        try
        {
            Console.WriteLine("Migrating database.");
            var migrator = new DbMigrator(databaseConnectionString, logger);
            var success = migrator.MigrateMsSqlDatabase(verbose);
            if (success)
            {
                Console.WriteLine("Migration successful.");
            }
            else
            {
                Console.WriteLine("Migration failed.");
            }
        }
        catch (SqlException e)
        {
            if (e.Message.Contains("Server is in script upgrade mode") && attempt < 10)
            {
                var nextAttempt = attempt + 1;
                Console.WriteLine("Database is in script upgrade mode. " +
                    "Trying again (attempt #{0})...", nextAttempt);
                Thread.Sleep(20000);
                MigrateDatabase(databaseConnectionString, verbose, nextAttempt);
                return;
            }
            throw;
        }
    }

    private static ILogger<DbMigrator> CreateLogger(bool verbose)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddConsole();

            if (verbose)
            {
                builder.AddFilter("DbMigrator.DbMigrator", LogLevel.Debug);
            }
            else
            {
                builder.AddFilter("DbMigrator.DbMigrator", LogLevel.Information);
            }
        });
        var logger = loggerFactory.CreateLogger<DbMigrator>();
        return logger;
    }
}
