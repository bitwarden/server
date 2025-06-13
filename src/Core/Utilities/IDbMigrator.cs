namespace Bit.Core.Utilities;

#nullable enable

public interface IDbMigrator
{
    bool MigrateDatabase(bool enableLogging = true,
        CancellationToken cancellationToken = default(CancellationToken));
}
