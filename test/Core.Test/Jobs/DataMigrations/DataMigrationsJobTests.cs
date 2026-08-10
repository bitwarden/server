#nullable enable

using Bit.Core.Jobs.DataMigrations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using Xunit;

namespace Bit.Core.Test.Jobs.DataMigrations;

public class DataMigrationsJobTests
{
    private readonly IDataMigration _migrationA = Substitute.For<IDataMigration>();
    private readonly IDataMigration _migrationB = Substitute.For<IDataMigration>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly DataMigrationsJob _sut;

    public DataMigrationsJobTests()
    {
        _context.CancellationToken.Returns(CancellationToken.None);
        _migrationA.Name.Returns("migration-a");
        _migrationB.Name.Returns("migration-b");
        _sut = new DataMigrationsJob([_migrationA, _migrationB],
            NullLogger<DataMigrationsJob>.Instance);
    }

    [Fact]
    public async Task Execute_RunsEveryRegisteredMigration()
    {
        await _sut.Execute(_context);

        await _migrationA.Received(1).RunAsync(Arg.Any<CancellationToken>());
        await _migrationB.Received(1).RunAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_MigrationsRunConcurrently_LongDrainCannotStarveSiblings()
    {
        // A finishes only after B has run — under sequential execution this deadlocks, so the
        // test hanging (and timing out) is the regression signal for a return to foreach/await.
        var bRan = new TaskCompletionSource();
        _migrationA.RunAsync(Arg.Any<CancellationToken>()).Returns(_ => bRan.Task);
        _migrationB.RunAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            bRan.SetResult();
            return Task.CompletedTask;
        });

        await _sut.Execute(_context);

        await _migrationA.Received(1).RunAsync(Arg.Any<CancellationToken>());
        await _migrationB.Received(1).RunAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OneMigrationThrows_OthersStillRun()
    {
        _migrationA.RunAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("boom"));

        await _sut.Execute(_context);

        await _migrationB.Received(1).RunAsync(Arg.Any<CancellationToken>());
    }
}
