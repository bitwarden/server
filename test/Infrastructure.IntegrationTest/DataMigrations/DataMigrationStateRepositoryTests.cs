using Bit.DataMigrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.DataMigrations;

public class DataMigrationStateRepositoryTests
{
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(1);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task CreateManyAsync_ConcurrentConstruction_OneSetWins(IServiceProvider services)
    {
        var repository = services.GetRequiredService<DataMigrationStateRepository>();
        var name = NewName();

        var results = await Task.WhenAll(
            repository.CreateManyAsync(Partitions(name, 3), Ct),
            repository.CreateManyAsync(Partitions(name, 2), Ct));

        var stored = await repository.GetManyAsync(name, Ct);
        Assert.Equal(stored.Select(s => s.Id), results[0].OrderBy(s => s.Partition).Select(s => s.Id));
        Assert.Equal(stored.Select(s => s.Id), results[1].OrderBy(s => s.Partition).Select(s => s.Id));
    }

    [Theory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task AcquireLeaseAsync_HeldByAnotherOwner_FailsUntilExpired(IServiceProvider services, TimeProvider timeProvider)
    {
        var repository = services.GetRequiredService<DataMigrationStateRepository>();
        var id = (await repository.CreateManyAsync(Partitions(NewName(), 1), Ct))[0].Id;

        Assert.NotNull(await repository.AcquireLeaseAsync(id, "a", _lease, true, Ct));
        Assert.Null(await repository.AcquireLeaseAsync(id, "b", _lease, true, Ct));
        Assert.NotNull(await repository.AcquireLeaseAsync(id, "a", _lease, true, Ct));

        ((FakeTimeProvider)timeProvider).Advance(_lease * 2);
        var acquired = await repository.AcquireLeaseAsync(id, "b", _lease, true, Ct);

        Assert.Equal("b", acquired?.LeaseOwner);
    }

    [Theory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task AcquireLeaseAsync_Paused_FailsOnlyWhenRespectingPause(IServiceProvider services)
    {
        var repository = services.GetRequiredService<DataMigrationStateRepository>();
        var name = NewName();
        var ids = (await repository.CreateManyAsync(Partitions(name, 2), Ct)).Select(s => s.Id).ToList();

        await repository.PauseAsync(name, Ct);
        Assert.All(await repository.GetManyAsync(name, Ct), s => Assert.NotNull(s.PausedDate));
        Assert.Null(await repository.AcquireLeaseAsync(ids[0], "job", _lease, true, Ct));
        Assert.NotNull(await repository.AcquireLeaseAsync(ids[0], "startup", _lease, false, Ct));

        await repository.ResumeAsync(name, Ct);
        Assert.All(await repository.GetManyAsync(name, Ct), s => Assert.Null(s.PausedDate));
        Assert.NotNull(await repository.AcquireLeaseAsync(ids[1], "job", _lease, true, Ct));
    }

    [Theory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task CheckpointAsync_FencedByOwnerAndPause(IServiceProvider services)
    {
        var repository = services.GetRequiredService<DataMigrationStateRepository>();
        var name = NewName();
        var id = (await repository.CreateManyAsync(Partitions(name, 1), Ct))[0].Id;
        await repository.AcquireLeaseAsync(id, "a", _lease, false, Ct);
        var checkpoint = new DataMigrationCheckpoint("cursor", 10, 7, 2, 1, false);

        Assert.False(await repository.CheckpointAsync(id, "b", checkpoint, _lease, false, Ct));
        Assert.True(await repository.CheckpointAsync(id, "a", checkpoint, _lease, false, Ct));
        await repository.PauseAsync(name, Ct);
        Assert.False(await repository.CheckpointAsync(id, "a", checkpoint, _lease, true, Ct));
        Assert.True(await repository.CheckpointAsync(id, "a", checkpoint with { Completed = true }, _lease, false, Ct));

        var state = Assert.Single(await repository.GetManyAsync(name, Ct));
        Assert.Equal(("cursor", 20L, 14L, 4L, 2L), (state.Cursor, state.RowsScanned, state.RowsConverted, state.RowsSkippedByRace, state.RowsFailed));
        Assert.NotNull(state.StartedDate);
        Assert.NotNull(state.CompletedDate);
    }

    [Theory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task ResetFailedAsync_RewindsOnlyPartitionsWithFailedRows(IServiceProvider services)
    {
        var repository = services.GetRequiredService<DataMigrationStateRepository>();
        var name = NewName();
        var ids = (await repository.CreateManyAsync(Partitions(name, 2), Ct)).Select(s => s.Id).ToList();
        await repository.PauseAsync(name, Ct);
        foreach (var (id, failed) in ids.Zip(new[] { 1L, 0L }))
        {
            await repository.AcquireLeaseAsync(id, "a", _lease, false, Ct);
            await repository.CheckpointAsync(id, "a", new DataMigrationCheckpoint("c", 5, 4, 0, failed, true), _lease, false, Ct);
        }

        await repository.ResetFailedAsync(name, Ct);

        var states = await repository.GetManyAsync(name, Ct);
        Assert.Equal(("a", 0L, 0L, 0L, 0L), (states[0].Cursor, states[0].RowsScanned, states[0].RowsConverted,
            states[0].RowsSkippedByRace, states[0].RowsFailed));
        Assert.Equal((null, null, null, null),
            (states[0].LeaseOwner, states[0].LeaseExpiresDate, states[0].StartedDate, states[0].CompletedDate));
        Assert.Equal(10, states[0].TotalRows);
        Assert.All(states, s => Assert.NotNull(s.PausedDate));
        Assert.Equal(("c", 5L), (states[1].Cursor, states[1].RowsScanned));
        Assert.NotNull(states[1].CompletedDate);
    }

    [Theory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task DeleteManyAsync_DeletesOnlyThatMigration(IServiceProvider services)
    {
        var repository = services.GetRequiredService<DataMigrationStateRepository>();
        var (name, other) = (NewName(), NewName());
        await repository.CreateManyAsync(Partitions(name, 2), Ct);
        await repository.CreateManyAsync(Partitions(other, 1), Ct);

        await repository.DeleteManyAsync(name, Ct);

        Assert.Empty(await repository.GetManyAsync(name, Ct));
        Assert.Single(await repository.GetManyAsync(other, Ct));
    }

    private static string NewName() => $"Test{Guid.NewGuid():N}";

    private static List<DataMigrationState> Partitions(string name, int count) =>
        Enumerable.Range(0, count).Select(i => new DataMigrationState
        {
            Name = name,
            Partition = i,
            RangeStart = "a",
            RangeEnd = "z",
            Cursor = "a",
            TotalRows = 10,
        }).ToList();
}
