using Bit.Migrator;
using CommandDotNet;

internal class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    [DefaultCommand]
    public int Execute(
        [Operand(Description = "Database connection string")]
        string databaseConnectionString,
        [Option('r', "repeatable", Description = "Mark scripts as repeatable")]
        bool repeatable = false,
        [Option('f', "folder", Description = "Folder name of database scripts")]
        string folderName = MigratorConstants.DefaultMigrationsFolderName,
        [Option('d', "dry-run", Description = "Print the scripts that will be applied without actually executing them")]
        bool dryRun = false,
        [Option("no-transaction", Description = "Run without adding transaction per script or all scripts")]
        bool noTransactionMigration = false
        )
    {
        return MigrateDatabase(databaseConnectionString, repeatable, folderName, dryRun, noTransactionMigration) ? 0 : -1;
    }


    private static bool MigrateDatabase(string databaseConnectionString,
        bool repeatable = false, string folderName = "", bool dryRun = false, bool noTransactionMigration = false)
    {
        var migrator = new DbMigrator(databaseConnectionString, noTransactionMigration: noTransactionMigration);
        bool success;
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(true, repeatable, folderName, dryRun: dryRun);
        }
        else
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(true, repeatable, dryRun: dryRun);
        }

        return success;
    }
}
