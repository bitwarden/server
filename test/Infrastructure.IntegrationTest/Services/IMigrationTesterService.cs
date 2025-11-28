namespace Bit.Infrastructure.IntegrationTest.Services;

/// <summary>
/// Defines the contract for applying a specific database migration across different database providers.
/// Implementations of this interface are responsible for migration execution logic,
/// and handling migration history to ensure that migrations can be tested independently and reliably.
/// </summary>
/// <remarks>
/// Each implementation should receive the migration name as a parameter in the constructor
/// to specify which migration is to be applied.
/// </remarks>
public interface IMigrationTesterService
{
    /// <summary>
    /// Applies the specified database migration.
    /// This may involve managing migration history and retry logic, depending on the implementation.
    /// </summary>
    void ApplyMigration();
}
