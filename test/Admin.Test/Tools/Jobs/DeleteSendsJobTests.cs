using Bit.Admin.Auth.Jobs;
using Bit.Admin.Tools.Jobs;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;

namespace Admin.Test.Tools.Jobs;

public class DeleteSendsJobTests
{
    // Matches the job's hardcoded batch size — a batch this size or larger reads as "more may
    // remain," which is what drives the job to fetch another batch.
    private const int BatchSize = 2000;

    private readonly ISendRepository _sendRepository;
    private readonly INonAnonymousSendCommand _nonAnonymousSendCommand;
    private readonly ILogger<DatabaseExpiredGrantsJob> _logger;
    private readonly DeleteSendsJob _sut;

    public DeleteSendsJobTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _nonAnonymousSendCommand = Substitute.For<INonAnonymousSendCommand>();
        _logger = Substitute.For<ILogger<DatabaseExpiredGrantsJob>>();

        _sut = new DeleteSendsJob(_sendRepository, BuildServiceProvider(_nonAnonymousSendCommand), _logger);

        _nonAnonymousSendCommand.DeleteManySendsAsync(Arg.Any<IEnumerable<Send>>())
            .Returns(callInfo => ((IEnumerable<Send>)callInfo[0]).Select(s => s.Id).ToList());
    }

    [Fact]
    public async Task Execute_NoSendsPending_DoesNothing()
    {
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(Array.Empty<Send>());

        await _sut.Execute(CreateContext());

        await _sendRepository.Received(1).GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>());
        await _nonAnonymousSendCommand.DidNotReceiveWithAnyArgs().DeleteManySendsAsync(default!);
    }

    [Fact]
    public async Task Execute_BatchSmallerThanBatchSize_StopsAfterOneIteration()
    {
        var sends = CreateSends(5);
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns(sends);

        await _sut.Execute(CreateContext());

        await _sendRepository.Received(1).GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>());
        await _nonAnonymousSendCommand.Received(1).DeleteManySendsAsync(sends);
    }

    [Fact]
    public async Task Execute_FullBatchThenShortBatch_LoopsUntilDrained()
    {
        var firstBatch = CreateSends(BatchSize);
        var secondBatch = CreateSends(500);
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(firstBatch, secondBatch);

        await _sut.Execute(CreateContext());

        await _sendRepository.Received(2).GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>());
        await _nonAnonymousSendCommand.Received(1).DeleteManySendsAsync(firstBatch);
        await _nonAnonymousSendCommand.Received(1).DeleteManySendsAsync(secondBatch);
        AssertLoggedContaining($"Deleted {BatchSize + 500} sends.");
    }

    [Fact]
    public async Task Execute_FullBatchWithNothingDeleted_StopsWithoutReReadingSameRows()
    {
        // A full batch where every Send was skipped (e.g. blob storage is unavailable) must not be
        // treated as "more may remain" — re-fetching would return the identical head-of-queue rows.
        var firstBatch = CreateSends(BatchSize);
        var secondBatch = CreateSends(BatchSize);
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(firstBatch, secondBatch);
        _nonAnonymousSendCommand.DeleteManySendsAsync(firstBatch).Returns(Array.Empty<Guid>());

        await _sut.Execute(CreateContext());

        await _sendRepository.Received(1).GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>());
        await _nonAnonymousSendCommand.DidNotReceive().DeleteManySendsAsync(secondBatch);
    }

    [Fact]
    public async Task Execute_CumulativeSkipsReachBatchSize_StopsEvenThoughEachBatchMadeSomeProgress()
    {
        // A partial outage can skip almost every send in a batch while still deleting one real
        // row, so the per-batch zero-progress guard never fires on its own. Once enough distinct
        // sends have been skipped across the run that the next fetch can only return already-known
        // -stuck rows, the loop must still stop rather than re-retrying them indefinitely.
        var firstBatch = CreateSends(BatchSize);
        var secondBatch = CreateSends(BatchSize);
        var thirdBatch = CreateSends(BatchSize);
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(firstBatch, secondBatch, thirdBatch);
        _nonAnonymousSendCommand.DeleteManySendsAsync(firstBatch).Returns(new[] { firstBatch[0].Id });
        _nonAnonymousSendCommand.DeleteManySendsAsync(secondBatch).Returns(new[] { secondBatch[0].Id });

        await _sut.Execute(CreateContext());

        await _sendRepository.Received(2).GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>());
        await _nonAnonymousSendCommand.DidNotReceive().DeleteManySendsAsync(thirdBatch);
    }

    [Fact]
    public async Task Execute_CancellationRequestedMidBatch_StopsBeforeNextFetch()
    {
        var firstBatch = CreateSends(BatchSize);
        var secondBatch = CreateSends(BatchSize);
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(firstBatch, secondBatch);

        using var cts = new CancellationTokenSource();
        _nonAnonymousSendCommand.DeleteManySendsAsync(firstBatch)
            .Returns(callInfo =>
            {
                cts.Cancel();
                return (ICollection<Guid>)firstBatch.Select(s => s.Id).ToList();
            });

        await _sut.Execute(CreateContext(cts.Token));

        await _sendRepository.Received(1).GetManyByDeletionDateAsync(Arg.Any<DateTime>(), Arg.Any<int>());
        await _nonAnonymousSendCommand.DidNotReceive().DeleteManySendsAsync(secondBatch);
    }

    [Fact]
    public async Task Execute_CancellationAlreadySignalled_DoesNothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await _sut.Execute(CreateContext(cts.Token));

        await _sendRepository.DidNotReceiveWithAnyArgs().GetManyByDeletionDateAsync(default, default);
        await _nonAnonymousSendCommand.DidNotReceiveWithAnyArgs().DeleteManySendsAsync(default!);
    }

    private void AssertLoggedContaining(string fragment) =>
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(fragment)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

    private static List<Send> CreateSends(int count) =>
        Enumerable.Range(0, count).Select(_ => new Send { Id = Guid.NewGuid(), Type = SendType.Text }).ToList();

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }

    private static IServiceProvider BuildServiceProvider(INonAnonymousSendCommand command)
    {
        var scopedProvider = Substitute.For<IServiceProvider>();
        scopedProvider.GetService(typeof(INonAnonymousSendCommand)).Returns(command);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scopedProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var rootProvider = Substitute.For<IServiceProvider>();
        rootProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
        return rootProvider;
    }
}
