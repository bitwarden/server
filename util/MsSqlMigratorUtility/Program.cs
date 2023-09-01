using Bit.Migrator;
using Microsoft.Extensions.Logging;
using CommandDotNet;

internal class Program
{
    private static IDictionary<string, string> Parameters { get; set; }

    private static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    [DefaultCommand]
    public void Execute(
        [Operand(Description = "Database connection string")]
        string databaseConnectionString,
        [Option('v', "verbose", Description = "Enable verbose output of migrator logs")]
        bool verbose = false,
        [Option('r', "rerunable", Description = "Mark scripts as rerunable")]
        bool rerunable = false,
        [Option('f', "folder", Description = "Folder name of database scripts")]
        string folderName = "DbScripts") => MigrateDatabase(databaseConnectionString, verbose, rerunable, folderName);

    private static void WriteUsageToConsole()
    {
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string>");
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string> -v|--verbose (for verbose output of migrator logs)");
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string> -r|--rerunable (for marking scripts as rerunable) -f|--folder <folder-name-in-migrator-project> (for specifying folder name of scripts)");
        Console.WriteLine("Usage: MsSqlMigratorUtility <database-connection-string> -v|--verbose (for verbose output of migrator logs) -r|--rerunable (for marking scripts as rerunable) -f|--folder <folder-name-in-migrator-project> (for specifying folder name of scripts)");
    }

    private static bool MigrateDatabase(string databaseConnectionString, bool verbose = false, bool rerunable = false, string folderName = "")
    {
        Console.WriteLine($"rerunable: {rerunable}");
        Console.WriteLine($"folderName: {folderName}");
        var logger = CreateLogger(verbose);

        var migrator = new DbMigrator(databaseConnectionString, logger);
        bool success = false;
        if (!string.IsNullOrWhiteSpace(folderName))
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
            if (i + 1 <= args.Length && !args[i + 1].StartsWith("-"))
            {
                Parameters.Add(args[i].Substring(1), args[i + 1]);
            }
            else
            {
                Parameters.Add(args[i].Substring(1), null);
            }
        }
    }
}
