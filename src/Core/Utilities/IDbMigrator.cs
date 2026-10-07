namespace Bit.Core.Utilities;

public interface IDbMigrator
{
    /// <param name="onDataMigration">
    /// Runs the named data migration at its marker and returns whether it completed. See
    /// <see cref="DataMigrationMarkers.Interleave"/>.
    /// </param>
    bool MigrateDatabase(bool enableLogging = true, Func<string, bool>? onDataMigration = null,
        CancellationToken cancellationToken = default(CancellationToken));
}
