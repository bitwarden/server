namespace Bit.Infrastructure.IntegrationTest.Services;

// WARNING: this has been disabled because it works by re-running migrations out of order.
// For MSSQL, this causes problems because the schema may have changed since the data migration was merged,
// so the data migration may fail.
// For EF, this causes problems because it reverts all subsequent migrations in order to re-run the data migration,
// which means the data migration should work but unrelated tests will now fail because the database is not up-to-date.
// This needs to be redesigned before use.

/// <summary>
/// Defines the contract for applying a specific database migration across different database providers.
/// Implementations of this interface are responsible for migration execution logic,
/// and handling migration history to ensure that migrations can be tested independently and reliably.
/// </summary>
/// <remarks>
/// Each implementation should receive the migration name as a parameter in the constructor
/// to specify which migration is to be applied.
/// </remarks>
[Obsolete("DO NOT USE as this interferes with subsequent database migrations.")]
public interface IMigrationTesterService
{
    /// <summary>
    /// Applies the specified database migration.
    /// This may involve managing migration history and retry logic, depending on the implementation.
    /// </summary>
    void ApplyMigration();
}
