using Bit.Admin;
using Bit.Admin.HostedServices;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Admin.Test.HostedServices;

public class DatabaseMigrationHostedServiceTests
{
    private readonly IDbMigrator _dbMigrator = Substitute.For<IDbMigrator>();
    private readonly ILogger<DatabaseMigrationHostedService> _logger =
        Substitute.For<ILogger<DatabaseMigrationHostedService>>();

    [Fact]
    public async Task StartAsync_WhenRunDatabaseMigrationsFalse_DoesNotCallMigrator()
    {
        var sut = new DatabaseMigrationHostedService(
            _dbMigrator,
            Options.Create(new AdminSettings { RunDatabaseMigrations = false }),
            _logger);

        await sut.StartAsync(CancellationToken.None);

        _dbMigrator.DidNotReceive().MigrateDatabase(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenRunDatabaseMigrationsUnset_DoesNotCallMigrator()
    {
        var sut = new DatabaseMigrationHostedService(
            _dbMigrator,
            Options.Create(new AdminSettings()),
            _logger);

        await sut.StartAsync(CancellationToken.None);

        _dbMigrator.DidNotReceive().MigrateDatabase(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // Proves the gate does not short-circuit when the flag is true: StartAsync must
    // reach the Task.Delay after the gate. A pre-cancelled token makes that delay
    // throw immediately, so the test runs in milliseconds. We deliberately do not
    // exercise the full migrate-and-retry path here — that would require waiting
    // through the real 20s delay or refactoring the service to inject a time
    // abstraction.
    [Fact]
    public async Task StartAsync_WhenRunDatabaseMigrationsTrue_WillRunMigrations()
    {
        var sut = new DatabaseMigrationHostedService(
            _dbMigrator,
            Options.Create(new AdminSettings { RunDatabaseMigrations = true }),
            _logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => sut.StartAsync(cts.Token));

        _dbMigrator.DidNotReceive().MigrateDatabase(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
