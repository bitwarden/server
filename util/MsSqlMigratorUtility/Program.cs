internal class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please enter a database connection string argument.");
            WriteUsageToConsole();
            return 1;
        }

        if (args.Length == 1 && (args[0] == "--verbose" || args[0] == "-v"))
        {
            Console.WriteLine($"Please enter a database connection string argument before {args[0]} option.");
            WriteUsageToConsole();
            return 1;
        }

        var databaseConnectionString = args[0];

        var verbose = false;

        if (args.Length == 2 && (args[1] == "--verbose" || args[1] == "-v"))
        {
            verbose = true;
        }

        var success = MigrateDatabase(databaseConnectionString, verbose);

        if (!success)
        {
            return -1;
        }

        return 0;
    }

    private static void WriteUsageToConsole()
    {
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string>");
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string> -v|--verbose (for verbose output of migrator logs)");
    }

    private static bool MigrateDatabase(string databaseConnectionString, bool verbose = false, int attempt = 1)
    {
        var logger = CreateLogger(verbose);

        var migrator = new DbMigrator(databaseConnectionString, logger);
        var success = migrator.MigrateMsSqlDatabaseWithRetries(verbose);

        return success;
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
