using Bit.Migrator;
using Microsoft.Extensions.Logging;

internal class Program
{
    private static IDictionary<string, string> Parameters { get; set; }

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

        ParseParameters(args);

        var verbose = false;

        if (Parameters.ContainsKey("--verbose") || Parameters.ContainsKey("-v"))
        {
            verbose = true;
        }

        var rerunable = false;

        if (Parameters.ContainsKey("--rerunable") || Parameters.ContainsKey("-r"))
        {
            rerunable = true;
        }


        var folderName = "";

        if (Parameters.ContainsKey("--folder") || Parameters.ContainsKey("-f"))
        {
            folderName = Parameters["--folder"] ?? Parameters["-f"];
        }

        var success = MigrateDatabase(databaseConnectionString, verbose, rerunable, folderName);

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
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string> -r|--rerunable (for marking scripts as rerunable) -f|--folder <folder-name-in-migrator-project> (for specifying folder name of scripts)");
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string> -v|--verbose (for verbose output of migrator logs) -r|--rerunable (for marking scripts as rerunable) -f|--folder <folder-name-in-migrator-project> (for specifying folder name of scripts)");
    }

    private static bool MigrateDatabase(string databaseConnectionString, bool verbose = false, bool rerunable = false, string folderName = "")
    {
        var logger = CreateLogger(verbose);

        var migrator = new DbMigrator(databaseConnectionString, logger);
        bool success = false;
        if (string.IsNullOrWhiteSpace(folderName))
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(verbose, rerunable, folderName);
        }
        else
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(verbose, rerunable);
        }

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

    private static void ParseParameters(string[] args)
    {
        Parameters = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("-"))
            {
                continue;
            }

            Parameters.Add(args[i].Substring(1), args[i + 1]);
        }
    }
}
