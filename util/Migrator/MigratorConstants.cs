namespace Bit.Migrator;

public static class MigratorConstants
{
    public const string SqlTableJournalName = "Migration";
    public const string DefaultMigrationsFolderName = "DbScripts";
    public const string TransitionMigrationsFolderName = "DbScripts_transition";
    public const int DefaultExecutionTimeoutMinutes = 5;
    public const int NoTransactionExecutionTimeoutMinutes = 60;
}
