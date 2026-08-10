#nullable enable

using Bit.Core.Jobs.DataMigrations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Jobs.DataMigrations;

public class BaseDataMigrationTests
{
    private record TestRow(Guid Id, string Value);
    private record TestUpdate(Guid Id, string OriginalValue, string NewValue);

    private class TestDataMigration : BaseDataMigration<TestRow, TestUpdate>
    {
        public bool EnabledValue { get; set; } = true;
        public int PartitionCountValue { get; set; } = 1;
        public Func<string?, string?, MigrationBatch<TestRow>> OnReadBatch { get; set; } =
            (_, _) => new MigrationBatch<TestRow>(Array.Empty<TestRow>(), null, 0, EndOfRange: true);
        public Func<string?, string?, Task<MigrationBatch<TestRow>>>? OnReadBatchAsync { get; set; }
        public Func<TestRow, TestUpdate?> OnShape { get; set; } =
            row => new TestUpdate(row.Id, row.Value, $"new-{row.Value}");
        public Func<IReadOnlyList<TestUpdate>, int> OnWriteBatch { get; set; } = updates => updates.Count;
        public List<IReadOnlyList<TestUpdate>> WrittenBatches { get; } = [];
        public long RowCount { get; set; }
        public Func<long, string?>? OnReadKeyAtOffset { get; set; } = offset => $"key-{offset:D3}";
        public List<long> SampledOffsets { get; } = [];

        public TestDataMigration(IDataMigrationStateRepository stateRepository)
            : base(stateRepository, TimeProvider.System, NullLogger<TestDataMigration>.Instance)
        { }

        public override string Name => "test-migration";
        protected override bool Enabled => EnabledValue;
        protected override int PartitionCount => PartitionCountValue;

        protected override Task<MigrationBatch<TestRow>> ReadBatchAsync(
            string? cursor, string? rangeEnd, CancellationToken token) =>
            OnReadBatchAsync?.Invoke(cursor, rangeEnd) ?? Task.FromResult(OnReadBatch(cursor, rangeEnd));

        protected override TestUpdate? Shape(TestRow row) => OnShape(row);

        protected override Task<long> CountRowsAsync(CancellationToken token) =>
            Task.FromResult(RowCount);

        protected override Task<string?> ReadKeyAtOffsetAsync(long offset, CancellationToken token)
        {
            if (OnReadKeyAtOffset == null)
            {
                return base.ReadKeyAtOffsetAsync(offset, token);
            }
            SampledOffsets.Add(offset);
            return Task.FromResult(OnReadKeyAtOffset(offset));
        }

        protected override Task<int> WriteBatchAsync(IReadOnlyList<TestUpdate> updates, CancellationToken token)
        {
            WrittenBatches.Add(updates);
            return Task.FromResult(OnWriteBatch(updates));
        }
    }

    private readonly IDataMigrationStateRepository _stateRepository =
        Substitute.For<IDataMigrationStateRepository>();
    private readonly TestDataMigration _sut;

    public BaseDataMigrationTests()
    {
        _stateRepository.ExistsAsync("test-migration", Arg.Any<CancellationToken>()).Returns(true);
        _stateRepository.CheckpointAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<MigrationCheckpoint>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        _stateRepository.ReadProgressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<PartitionProgress>());
        _sut = new TestDataMigration(_stateRepository);
    }

    private static PartitionClaim EmptyClaim(int partition = 0) =>
        new(partition, null, null, null, 0, 0, 0, 0, 0, null);

    [Fact]
    public async Task RunAsync_MigrationDisabled_DoesNothing()
    {
        _sut.EnabledValue = false;

        await _sut.RunAsync(CancellationToken.None);

        await _stateRepository.DidNotReceiveWithAnyArgs().TryClaimAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task RunAsync_NotInitialized_InitializesPartitionsBeforeClaiming()
    {
        _stateRepository.ExistsAsync("test-migration", Arg.Any<CancellationToken>()).Returns(false);
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()).Returns((PartitionClaim?)null);
        _sut.RowCount = 42;

        await _sut.RunAsync(CancellationToken.None);

        // Single partition: the full open range, anchored with the table's row count.
        await _stateRepository.Received(1).InitializeAsync("test-migration",
            Arg.Is<IReadOnlyList<PartitionRange>>(p =>
                p.Count == 1 && p[0] == new PartitionRange(0, null, null, 42)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NotInitialized_MultiPartition_SamplesPercentileBoundaries()
    {
        _stateRepository.ExistsAsync("test-migration", Arg.Any<CancellationToken>()).Returns(false);
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()).Returns((PartitionClaim?)null);
        _sut.PartitionCountValue = 4;
        _sut.RowCount = 100;

        await _sut.RunAsync(CancellationToken.None);

        // 100 rows / 4 partitions → the last zero-based row of each 25-row slice.
        Assert.Equal(new long[] { 24, 49, 74 }, _sut.SampledOffsets);
        await _stateRepository.Received(1).InitializeAsync("test-migration",
            Arg.Is<IReadOnlyList<PartitionRange>>(p =>
                p.Count == 4 &&
                p[0] == new PartitionRange(0, null, "key-024", 25) &&
                p[1] == new PartitionRange(1, "key-024", "key-049", 25) &&
                p[2] == new PartitionRange(2, "key-049", "key-074", 25) &&
                p[3] == new PartitionRange(3, "key-074", null, 25)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NotInitialized_TableSmallerThanPartitionCount_MergesSlices()
    {
        _stateRepository.ExistsAsync("test-migration", Arg.Any<CancellationToken>()).Returns(false);
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()).Returns((PartitionClaim?)null);
        _sut.PartitionCountValue = 4;
        _sut.RowCount = 2;

        await _sut.RunAsync(CancellationToken.None);

        // Offsets collapse to [-1, 0, 0]; only the first 0 is sampled, merging adjacent slices.
        Assert.Equal(new long[] { 0 }, _sut.SampledOffsets);
        await _stateRepository.Received(1).InitializeAsync("test-migration",
            Arg.Is<IReadOnlyList<PartitionRange>>(p =>
                p.Count == 2 &&
                p[0] == new PartitionRange(0, null, "key-000", 1) &&
                p[1] == new PartitionRange(1, "key-000", null, 1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NotInitialized_EmptyTable_InitializesSingleFullRange()
    {
        _stateRepository.ExistsAsync("test-migration", Arg.Any<CancellationToken>()).Returns(false);
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()).Returns((PartitionClaim?)null);
        _sut.PartitionCountValue = 4;
        _sut.RowCount = 0;

        await _sut.RunAsync(CancellationToken.None);

        Assert.Empty(_sut.SampledOffsets);
        await _stateRepository.Received(1).InitializeAsync("test-migration",
            Arg.Is<IReadOnlyList<PartitionRange>>(p =>
                p.Count == 1 && p[0] == new PartitionRange(0, null, null, 0)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NotInitialized_MultiPartitionWithoutKeyOffsetOverride_Throws()
    {
        _stateRepository.ExistsAsync("test-migration", Arg.Any<CancellationToken>()).Returns(false);
        _sut.PartitionCountValue = 4;
        _sut.RowCount = 100;
        _sut.OnReadKeyAtOffset = null;

        await Assert.ThrowsAsync<NotSupportedException>(() => _sut.RunAsync(CancellationToken.None));

        await _stateRepository.DidNotReceiveWithAnyArgs().InitializeAsync(default!, default!, default);
    }

    [Fact]
    public async Task RunAsync_EveryFiring_ReadsProgressAcrossAllPartitions()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()).Returns((PartitionClaim?)null);
        _stateRepository.ReadProgressAsync("test-migration", Arg.Any<CancellationToken>())
            .Returns(new List<PartitionProgress>
            {
                new(0, 100, 40, false),
                new(1, 100, 100, true),
            });

        await _sut.RunAsync(CancellationToken.None);

        // The pending-rows tally runs even when every partition is leased elsewhere (no claim).
        await _stateRepository.Received(1).ReadProgressAsync("test-migration",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NoClaimAvailable_DoesNotProcess()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()).Returns((PartitionClaim?)null);

        await _sut.RunAsync(CancellationToken.None);

        Assert.Empty(_sut.WrittenBatches);
        await _stateRepository.DidNotReceiveWithAnyArgs().CheckpointAsync(
            default!, default, default!, default!, default, default);
    }

    [Fact]
    public async Task RunAsync_HappyPath_WritesThenCheckpointsThenReleases()
    {
        var rowId = Guid.NewGuid();
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        var readCursors = new List<string?>();
        _sut.OnReadBatch = (cursor, _) =>
        {
            readCursors.Add(cursor);
            return readCursors.Count == 1
                ? new MigrationBatch<TestRow>([new TestRow(rowId, "a")], rowId.ToString(),
                    ScannedCount: 5, EndOfRange: false)
                : new MigrationBatch<TestRow>(Array.Empty<TestRow>(), "end",
                    ScannedCount: 2, EndOfRange: true);
        };

        await _sut.RunAsync(CancellationToken.None);

        // The drain continues from the checkpointed cursor within the same claim — it does not
        // wait for the next firing.
        Assert.Equal(new string?[] { null, rowId.ToString() }, readCursors);
        Assert.Single(_sut.WrittenBatches);
        await _stateRepository.Received(1).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Is<MigrationCheckpoint>(c =>
                c.Cursor == rowId.ToString() &&
                c.RowsScanned == 5 &&
                c.RowsConverted == 1 &&
                c.RowsSkippedByRace == 0 &&
                c.RowsFailed == 0 &&
                c.CompletedDate == null),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        // The final batch checkpoints cumulative totals and stamps completion.
        await _stateRepository.Received(1).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Is<MigrationCheckpoint>(c =>
                c.Cursor == "end" && c.RowsScanned == 7 && c.CompletedDate != null),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _stateRepository.Received(1).ReleaseAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DrainsPartition_AccumulatesCountersAndRenewsLeasePerBatch()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        var reads = 0;
        _sut.OnReadBatch = (_, _) =>
        {
            reads++;
            return reads switch
            {
                1 => new MigrationBatch<TestRow>([new TestRow(Guid.NewGuid(), "a")], "c1", 5, false),
                2 => new MigrationBatch<TestRow>([new TestRow(Guid.NewGuid(), "b")], "c2", 3, false),
                _ => new MigrationBatch<TestRow>(Array.Empty<TestRow>(), "c3", 2, EndOfRange: true),
            };
        };

        await _sut.RunAsync(CancellationToken.None);

        // One claim drains the whole range: three batches, two writes, no re-claims.
        Assert.Equal(3, reads);
        Assert.Equal(2, _sut.WrittenBatches.Count);
        await _stateRepository.Received(1).TryClaimAsync("test-migration", Arg.Any<string>(),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        // Every checkpoint renews the lease for another LeaseDuration.
        await _stateRepository.Received(3).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Any<MigrationCheckpoint>(), TimeSpan.FromMinutes(10), Arg.Any<CancellationToken>());
        // Counters accumulate across batches within the claim.
        await _stateRepository.Received(1).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Is<MigrationCheckpoint>(c =>
                c.Cursor == "c3" &&
                c.RowsScanned == 10 &&
                c.RowsConverted == 2 &&
                c.CompletedDate != null),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _stateRepository.Received(1).ReleaseAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_OverlappingExecutions_RespectDrainCapAcrossFirings()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        var parked = new TaskCompletionSource();
        var resume = new TaskCompletionSource();
        _sut.OnReadBatchAsync = async (_, _) =>
        {
            parked.TrySetResult();
            await resume.Task;
            return new MigrationBatch<TestRow>(Array.Empty<TestRow>(), "c", 0, EndOfRange: true);
        };

        var first = _sut.RunAsync(CancellationToken.None);
        try
        {
            await parked.Task;

            // A second firing overlaps while the first is mid-drain: with MaxParallelBatches = 1
            // the process-wide slot is taken, so it must exit without even attempting a claim.
            await new TestDataMigration(_stateRepository).RunAsync(CancellationToken.None);

            await _stateRepository.Received(1).TryClaimAsync("test-migration", Arg.Any<string>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            resume.TrySetResult();
            await first;
        }
    }

    [Fact]
    public async Task RunAsync_DisabledMidDrain_StopsBetweenBatches()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        _sut.OnReadBatch = (_, _) =>
        {
            // The kill switch flips while the partition is mid-drain.
            _sut.EnabledValue = false;
            return new MigrationBatch<TestRow>([new TestRow(Guid.NewGuid(), "a")], "c1", 1, EndOfRange: false);
        };

        await _sut.RunAsync(CancellationToken.None);

        // The in-flight batch finishes cleanly (written + checkpointed); the next iteration
        // observes the flag, stops, and releases — no waiting for the next firing.
        Assert.Single(_sut.WrittenBatches);
        await _stateRepository.Received(1).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Any<MigrationCheckpoint>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _stateRepository.Received(1).ReleaseAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_EndOfRange_StampsCompletedDate()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        _sut.OnReadBatch = (_, _) => new MigrationBatch<TestRow>(
            Array.Empty<TestRow>(), "cursor-end", 3, EndOfRange: true);
        _stateRepository.ReadIncompleteCountAsync("test-migration", Arg.Any<CancellationToken>())
            .Returns(0);

        await _sut.RunAsync(CancellationToken.None);

        await _stateRepository.Received(1).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Is<MigrationCheckpoint>(c => c.CompletedDate != null),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CasMisses_CountsRaced()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        _sut.OnReadBatch = (_, _) => new MigrationBatch<TestRow>(
            [new TestRow(Guid.NewGuid(), "a"), new TestRow(Guid.NewGuid(), "b")], "c", 2, EndOfRange: true);
        _sut.OnWriteBatch = _ => 1; // one of two updates lost its race

        await _sut.RunAsync(CancellationToken.None);

        await _stateRepository.Received(1).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Is<MigrationCheckpoint>(c => c.RowsConverted == 1 && c.RowsSkippedByRace == 1),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ShapeThrows_CountsFailedAndContinues()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        _sut.OnReadBatch = (_, _) => new MigrationBatch<TestRow>(
            [new TestRow(Guid.NewGuid(), "bad"), new TestRow(Guid.NewGuid(), "good")], "c", 2, EndOfRange: true);
        _sut.OnShape = row => row.Value == "bad"
            ? throw new InvalidOperationException("undecodable")
            : new TestUpdate(row.Id, row.Value, "new");

        await _sut.RunAsync(CancellationToken.None);

        await _stateRepository.Received(1).CheckpointAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Is<MigrationCheckpoint>(c => c.RowsFailed == 1 && c.RowsConverted == 1),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LeaseLost_StillReleases()
    {
        _stateRepository.TryClaimAsync("test-migration", Arg.Any<string>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyClaim(), (PartitionClaim?)null);
        _stateRepository.CheckpointAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<MigrationCheckpoint>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        _sut.OnReadBatch = (_, _) => new MigrationBatch<TestRow>(
            [new TestRow(Guid.NewGuid(), "a")], "c", 1, false);

        await _sut.RunAsync(CancellationToken.None);

        // Fenced release is safe to attempt even when the lease was lost.
        await _stateRepository.Received(1).ReleaseAsync("test-migration", 0, Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        // Completion must not be evaluated on a fenced-out checkpoint.
        await _stateRepository.DidNotReceive().ReadIncompleteCountAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
