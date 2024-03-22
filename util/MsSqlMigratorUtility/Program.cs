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
        string folderName = MigratorConstants.DefaultMigrationsFolderName)
        => MigrateDatabase(databaseConnectionString, repeatable, folderName);

    private static bool MigrateDatabase(string databaseConnectionString,
        bool repeatable = false, string folderName = "")
    {
        var migrator = new DbMigrator(databaseConnectionString);
        bool success;
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(true, repeatable, folderName);
        }
        else
        {
            success = migrator.MigrateMsSqlDatabaseWithRetries(true, repeatable);
        }

        return success;
    }
}
