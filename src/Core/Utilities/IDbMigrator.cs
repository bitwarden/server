namespace Bit.Core.Utilities;

public interface IDbMigrator
{
    bool MigrateDatabase(
        bool enableLogging = true,
        CancellationToken cancellationToken = default(CancellationToken)
    );
}
