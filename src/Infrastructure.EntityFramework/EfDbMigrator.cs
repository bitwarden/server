using Bit.Core;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.EntityFramework;

public class EfDbMigrator(IServiceScopeFactory serviceScopeFactory, ILogger<EfDbMigrator> logger) : IDbMigrator
{
    public bool MigrateDatabase(bool enableLogging = true, Func<string, bool>? onDataMigration = null,
        CancellationToken cancellationToken = default)
    {
        if (enableLogging)
        {
            logger.LogInformation(Constants.BypassFiltersEventId, "Migrating database.");
        }

        using var scope = serviceScopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>().Database;
        var migrator = database.GetService<IMigrator>();
        var applied = database.GetAppliedMigrations().ToList();
        var pending = database.GetPendingMigrations().ToList();
        var success = DataMigrationMarkers.Interleave(pending, segment =>
        {
            // Migrating to a target reverts every applied migration after it, so only the last segment may run
            // when one was applied out of order.
            if (segment[^1] == pending[^1])
            {
                migrator.Migrate();
                return true;
            }

            if (applied.FirstOrDefault(a => string.CompareOrdinal(a, segment[^1]) > 0) is { } newer)
            {
                logger.LogError(Constants.BypassFiltersEventId,
                    "Can't migrate to {Target} without reverting {Applied}, which was applied out of order.", segment[^1], newer);
                return false;
            }

            migrator.Migrate(segment[^1]);
            return true;
        }, onDataMigration, logger);

        if (enableLogging && success)
        {
            logger.LogInformation(Constants.BypassFiltersEventId, "Migration successful.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return success;
    }
}
