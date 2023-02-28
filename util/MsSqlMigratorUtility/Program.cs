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

        var migrator = new DbMigrator(databaseConnectionString, logger);
        var success = migrator.MigrateMsSqlDatabase(verbose);
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
