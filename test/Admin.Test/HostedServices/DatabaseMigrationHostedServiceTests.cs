using System.Data.Common;
using Bit.Admin;
using Bit.Admin.HostedServices;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Admin.Test.HostedServices;

public class DatabaseMigrationHostedServiceTests
{
    private static readonly TimeSpan _migrationDelay = TimeSpan.FromSeconds(20);

    private readonly IDbMigrator _dbMigrator = Substitute.For<IDbMigrator>();
    private readonly ILogger<DatabaseMigrationHostedService> _logger =
        Substitute.For<ILogger<DatabaseMigrationHostedService>>();
    private readonly FakeTimeProvider _timeProvider = new();

    private DatabaseMigrationHostedService BuildSut(AdminSettings adminSettings) =>
        new(_dbMigrator, Options.Create(adminSettings), _timeProvider, _logger);

    [Fact]
    public async Task StartAsync_WhenRunDatabaseMigrationsFalse_DoesNotCallMigrator()
    {
        var sut = BuildSut(new AdminSettings { RunDatabaseMigrations = false });

        await sut.StartAsync(CancellationToken.None);

        _dbMigrator.DidNotReceive().MigrateDatabase(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenRunDatabaseMigrationsUnset_DoesNotCallMigrator()
    {
        var sut = BuildSut(new AdminSettings());

        await sut.StartAsync(CancellationToken.None);

        _dbMigrator.DidNotReceive().MigrateDatabase(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenRunDatabaseMigrationsTrue_CallsMigratorAfterInitialDelay()
    {
        var sut = BuildSut(new AdminSettings { RunDatabaseMigrations = true });

        var startTask = sut.StartAsync(CancellationToken.None);
        _timeProvider.Advance(_migrationDelay);
        await startTask;

        _dbMigrator.Received(1).MigrateDatabase(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenMigratorThrowsDbException_RetriesAfterDelay()
    {
        var sut = BuildSut(new AdminSettings { RunDatabaseMigrations = true });
        var attempt = 0;
        _dbMigrator
            .When(x => x.MigrateDatabase(true, Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                attempt++;
                if (attempt == 1)
                {
                    throw Substitute.For<DbException>();
                }
            });

        var startTask = sut.StartAsync(CancellationToken.None);
        _timeProvider.Advance(_migrationDelay); // initial delay
        _timeProvider.Advance(_migrationDelay); // retry delay after first failure
        await startTask;

        _dbMigrator.Received(2).MigrateDatabase(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenMigratorAlwaysThrowsDbException_RethrowsAfterMaxAttempts()
    {
        var sut = BuildSut(new AdminSettings { RunDatabaseMigrations = true });
        _dbMigrator
            .When(x => x.MigrateDatabase(true, Arg.Any<CancellationToken>()))
            .Throw(_ => Substitute.For<DbException>());

        var startTask = sut.StartAsync(CancellationToken.None);
        for (var i = 0; i < 10; i++)
        {
            _timeProvider.Advance(_migrationDelay);
        }

        await Assert.ThrowsAnyAsync<DbException>(() => startTask);
        _dbMigrator.Received(10).MigrateDatabase(true, Arg.Any<CancellationToken>());
    }
}
