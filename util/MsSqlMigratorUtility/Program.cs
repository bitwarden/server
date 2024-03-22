using Bit.Migrator;
using CommandDotNet;

internal class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    [DefaultCommand]
    public void Execute(
        [Operand(Description = "Database connection string")]
        string databaseConnectionString,
        [Option('r', "repeatable", Description = "Mark scripts as repeatable")]
        bool repeatable = false,
        [Option('f', "folder", Description = "Folder name of database scripts")]
        string folderName = MigratorConstants.DefaultMigrationsFolderName,
        [Option("dry-run", Description = "Print the scripts that will be applied without actually executing them")]
        bool dryRun = false
        ) => MigrateDatabase(databaseConnectionString, repeatable, folderName, dryRun);

    private static bool MigrateDatabase(string databaseConnectionString,
        bool repeatable = false, string folderName = "", bool dryRun = false)
    {
        var migrator = new DbMigrator(databaseConnectionString);
        bool success;
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(true, repeatable, folderName, dryRun);
        }
        else
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(true, repeatable, dryRun: dryRun);
        }

        return success;
    }
}
