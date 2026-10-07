using Bit.DataMigrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.DataMigrations;

public class DataMigrationRunnerTests
{
    private static readonly DataMigrationRunOptions _job = new("job", Scheduled: true);
    private static readonly DataMigrationRunOptions _startup = new("startup");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory, DatabaseData]
    public async Task EnsurePartitionsAsync_SplitsIntoPausedPartitions(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(10));
        var migration = new SampleMigration(table, partitionCount: 3);
        var runner = Runner(services, migration);

        await runner.EnsurePartitionsAsync(migration.Name, Ct);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);

        var partitions = await Repository(services).GetManyAsync(migration.Name, Ct);
        Assert.Equal([4L, 4L, 2L], partitions.Select(p => p.TotalRows));
        Assert.All(partitions, p => Assert.NotNull(p.PausedDate));
        Assert.All(await runner.RunAsync(migration.Name, _job, Ct), r => Assert.Equal(PartitionRunStatus.Stopped, r.Status));
    }

    [Theory, DatabaseData]
    public async Task RunAsync_AsTheJob_ConvertsEveryPartitionOnceStarted(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(10));
        var migration = new SampleMigration(table, partitionCount: 3, maxParallelBatches: 3);
        var runner = Runner(services, migration);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);
        await Repository(services).ResumeAsync(migration.Name, Ct);

        Assert.All(await runner.RunAsync(migration.Name, _job, Ct), r => Assert.Equal(PartitionRunStatus.Completed, r.Status));

        Assert.All(await table.ReadAllAsync(), r => Assert.Equal(r.Value!.ToUpperInvariant(), r.Value));
        Assert.Empty(await runner.RunAsync(migration.Name, _job, Ct));
    }

    [Theory, DatabaseData]
    public async Task RunToCompletionAsync_ConvertsEveryRowOnceAcrossPartitions(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(10).Append("DONE").Append("ALSO"));
        var migration = new SampleMigration(table, partitionCount: 3, maxParallelBatches: 3);

        Assert.True(await Runner(services, migration).RunToCompletionAsync(migration.Name, Ct));

        Assert.All(await table.ReadAllAsync(), r => Assert.Equal(r.Value?.ToUpperInvariant(), r.Value));
        var partitions = await Repository(services).GetManyAsync(migration.Name, Ct);
        Assert.All(partitions, p => Assert.NotNull(p.CompletedDate));
        Assert.Equal((12L, 10L, 2L), (partitions.Sum(p => p.RowsScanned), partitions.Sum(p => p.RowsConverted),
            partitions.Sum(p => p.RowsSkippedByRace)));
    }

    [Theory, DatabaseData]
    public async Task RunToCompletionAsync_EmptyTable_CompletesWithoutPartitions(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, []);
        var migration = new SampleMigration(table, partitionCount: 3);

        Assert.True(await Runner(services, migration).RunToCompletionAsync(migration.Name, Ct));

        Assert.Empty(await Repository(services).GetManyAsync(migration.Name, Ct));
    }

    [Theory, DatabaseData]
    public async Task RunToCompletionAsync_FailedRows_LoggedByKeyAndOthersConvert(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, ["a", "throw", "b", "cas-throw", "c"]);
        var migration = new SampleMigration(table, batchSize: 10);
        var logs = services.GetFakeLogCollector();

        Assert.False(await Runner(services, migration).RunToCompletionAsync(migration.Name, Ct));

        var rows = (await table.ReadAllAsync()).ToDictionary(r => r.Value!, r => r.Id);
        Assert.Equal(["A", "B", "C", "cas-throw", "throw"], rows.Keys.Order(StringComparer.Ordinal));
        var partition = Assert.Single(await Repository(services).GetManyAsync(migration.Name, Ct));
        Assert.Equal((3L, 2L), (partition.RowsConverted, partition.RowsFailed));
        var messages = logs.GetSnapshot().Where(l => l.Category == typeof(DataMigrationRunner).FullName).Select(l => l.Message).ToList();
        var failures = messages.Where(m => m.Contains("failed to convert row")).ToList();
        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, m => m.Contains(rows["throw"].ToString()));
        Assert.Contains(failures, m => m.Contains(rows["cas-throw"].ToString()));
        Assert.DoesNotContain(messages, m => m.Contains("cas-throw") || m.Contains("boom"));
    }

    [Theory, DatabaseData]
    public async Task ResetAsync_SplitsAgain_ConvertingRowsWrittenSinceAndSkippingTheRest(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(5));
        var migration = new SampleMigration(table);
        var runner = Runner(services, migration);
        await runner.RunToCompletionAsync(migration.Name, Ct);
        await table.InsertAsync(Values(3));

        await runner.ResetAsync(migration.Name, Ct);
        var partition = Assert.Single(await Repository(services).GetManyAsync(migration.Name, Ct));
        var result = Assert.Single(await runner.RunAsync(migration.Name, _startup, Ct));

        Assert.Equal(8, partition.TotalRows);
        Assert.NotNull(partition.PausedDate);
        Assert.Equal((PartitionRunStatus.Completed, 8L, 3L, 5L), (result.Status, result.Scanned, result.Converted, result.Skipped));
    }

    [Theory, DatabaseData]
    public async Task RunAsync_OnePartitionThrows_OthersComplete(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(9));
        var migration = new SampleMigration(table, partitionCount: 3, maxParallelBatches: 3);
        var runner = Runner(services, migration);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DbContext>().Set<DataMigrationState>()
                .Where(s => s.Name == migration.Name && s.Partition == 1)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Cursor, "not-a-key"), Ct);
        }

        var results = await runner.RunAsync(migration.Name, _startup, Ct);

        Assert.Equal([PartitionRunStatus.Completed, PartitionRunStatus.Error, PartitionRunStatus.Completed], results.Select(r => r.Status));
    }

    [Theory, DatabaseData]
    public async Task RunAsync_RecordsMetricsPerBatch(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, ["a", "B", "throw", "c"]);
        var migration = new SampleMigration(table);
        var runner = Runner(services, migration);
        var (rows, pending, duration) = Collectors(services);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);

        await runner.RunAsync(migration.Name, _startup, Ct);

        var outcomes = rows.GetMeasurementSnapshot().GroupBy(m => m.Tags["migration.outcome"]).ToDictionary(g => g.Key!, g => g.Sum(m => m.Value));
        Assert.Equal(new Dictionary<object, long> { ["Succeeded"] = 2, ["Skipped"] = 1, ["Failed"] = 1 }, outcomes);
        Assert.Equal([1L, 0L], pending.GetMeasurementSnapshot().Select(m => m.Value));
        Assert.Equal(2, duration.GetMeasurementSnapshot().Count);
        Assert.All(rows.GetMeasurementSnapshot().Concat(pending.GetMeasurementSnapshot()), m =>
        {
            Assert.Equal(migration.Name, m.Tags["migration.name"]);
            Assert.Equal("0", m.Tags["migration.partition.name"]);
        });
    }

    [Theory, DatabaseData]
    public async Task RunAsync_MaxBatches_CheckpointsAndResumes(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(10));
        var migration = new SampleMigration(table);
        var runner = Runner(services, migration);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);

        var first = Assert.Single(await runner.RunAsync(migration.Name, _startup with { MaxBatches = 2 }, Ct));
        var second = Assert.Single(await runner.RunAsync(migration.Name, _startup, Ct));

        Assert.Equal((PartitionRunStatus.BatchLimit, 6L, 2), (first.Status, first.Converted, first.BatchDurations.Count));
        Assert.Equal((PartitionRunStatus.Completed, 4L), (second.Status, second.Converted));
    }

    [Theory, DatabaseData]
    public async Task RunAsync_DryRun_WritesNothing(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, ["a", "B", "throw"]);
        var migration = new SampleMigration(table);
        var runner = Runner(services, migration);
        var (rows, pending, duration) = Collectors(services);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);
        var before = (await table.ReadAllAsync(), await Repository(services).GetManyAsync(migration.Name, Ct));

        var result = Assert.Single(await runner.RunAsync(migration.Name, _job with { DryRun = true }, Ct));

        Assert.Equal((PartitionRunStatus.Completed, 1L, 1L, 1L), (result.Status, result.Converted, result.Skipped, result.Failed));
        Assert.Equal(before.Item1.OrderBy(r => r.Id), (await table.ReadAllAsync()).OrderBy(r => r.Id));
        Assert.Equivalent(before.Item2, await Repository(services).GetManyAsync(migration.Name, Ct));
        Assert.Empty(rows.GetMeasurementSnapshot().Concat<object>(pending.GetMeasurementSnapshot()).Concat(duration.GetMeasurementSnapshot()));
    }

    [Theory, DatabaseData]
    public async Task RunAsync_RowChangedAfterRead_KeepsNewValue(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(3));
        var raced = (await table.ReadAllAsync()).Single(r => r.Value == "v0").Id;
        var migration = new SampleMigration(table)
        {
            OnTransform = value =>
            {
                if (value == "v0")
                {
                    table.UpdateAsync(raced, "changed").GetAwaiter().GetResult();
                }
            },
        };
        var runner = Runner(services, migration);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);

        var result = Assert.Single(await runner.RunAsync(migration.Name, _startup, Ct));

        Assert.Equal((2L, 1L), (result.Converted, result.Skipped));
        Assert.Equal("changed", (await table.ReadAllAsync()).Single(r => r.Id == raced).Value);
    }

    [Theory, DatabaseData]
    public async Task RunAsync_PausedMidRun_JobStopsAndStartupFinishes(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(6));
        var repository = Repository(services);
        var migration = new SampleMigration(table)
        {
            OnTransform = _ => repository.PauseAsync(table.Name).GetAwaiter().GetResult(),
        };
        var runner = Runner(services, migration);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);
        await repository.ResumeAsync(migration.Name, Ct);

        Assert.Equal(PartitionRunStatus.Stopped, Assert.Single(await runner.RunAsync(migration.Name, _job, Ct)).Status);
        Assert.True(await runner.RunToCompletionAsync(migration.Name, Ct));
    }

    [Theory, DatabaseData]
    public async Task RunToCompletionAsync_WaitsOutAnotherOwnersLease(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, Values(3));
        var migration = new SampleMigration(table);
        var runner = Runner(services, migration);
        await runner.EnsurePartitionsAsync(migration.Name, Ct);
        var partition = Assert.Single(await Repository(services).GetManyAsync(migration.Name, Ct));
        await Repository(services).AcquireLeaseAsync(partition.Id, "other", TimeSpan.FromSeconds(2), false, Ct);

        Assert.Equal(PartitionRunStatus.Stopped, Assert.Single(await runner.RunAsync(migration.Name, _startup, Ct)).Status);
        Assert.True(await runner.RunToCompletionAsync(migration.Name, Ct));
    }

    [Theory, DatabaseData]
    public async Task RunAsync_NullValues_AreReadAndWritten(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, [null, "to-null", null]);
        var migration = new SampleMigration(table);

        Assert.True(await Runner(services, migration).RunToCompletionAsync(migration.Name, Ct));

        Assert.Equal([null, "was-null", "was-null"], (await table.ReadAllAsync()).Select(r => r.Value).Order());
    }

    [Theory, DatabaseData]
    public async Task RunAsync_ValueTooLongForItsColumn_IsNeverTruncated(IServiceProvider services)
    {
        await using var table = await ScratchTable.CreateAsync(services, ["too-long", "a"]);
        var migration = new SampleMigration(table);

        await Runner(services, migration).RunToCompletionAsync(migration.Name, Ct);

        Assert.Contains(await table.ReadAllAsync(), r => r.Value == "A");
        Assert.DoesNotContain(await table.ReadAllAsync(), r => r.Value == new string('X', 50));
    }

    private static IEnumerable<string?> Values(int count) => Enumerable.Range(0, count).Select(i => $"v{i}");

    private static DataMigrationStateRepository Repository(IServiceProvider services) =>
        services.GetRequiredService<DataMigrationStateRepository>();

    private static DataMigrationRunner Runner(IServiceProvider services, DataMigration migration)
    {
        var runner = ActivatorUtilities.CreateInstance<DataMigrationRunner>(services, (object)new[] { migration });
        runner.RetryDelay = runner.ErrorDelay = TimeSpan.FromMilliseconds(200);
        return runner;
    }

    private static (MetricCollector<long>, MetricCollector<long>, MetricCollector<double>) Collectors(IServiceProvider services)
    {
        var factory = services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
        return (new(factory, DataMigrationMetrics.MeterName, "bitwarden.datamigration.rows"),
            new(factory, DataMigrationMetrics.MeterName, "bitwarden.datamigration.pending_rows"),
            new(factory, DataMigrationMetrics.MeterName, "bitwarden.datamigration.batch.duration"));
    }
}
