using Bit.Admin.Jobs;
using Bit.Core;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace Admin.Test.Jobs;

public class CleanUpOrganizationEventsJobTests
{
    private readonly IOrganizationEventCleanupRepository _cleanupRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IFeatureService _featureService;
    private readonly ILogger<CleanUpOrganizationEventsJob> _logger;
    private readonly CleanUpOrganizationEventsJob _sut;

    public CleanUpOrganizationEventsJobTests()
    {
        _cleanupRepository = Substitute.For<IOrganizationEventCleanupRepository>();
        _eventRepository = Substitute.For<IEventRepository>();
        _featureService = Substitute.For<IFeatureService>();
        _logger = Substitute.For<ILogger<CleanUpOrganizationEventsJob>>();
        _sut = new CleanUpOrganizationEventsJob(
            _cleanupRepository,
            _eventRepository,
            _featureService,
            _logger);

        _featureService.IsEnabled(FeatureFlagKeys.OrganizationEventCleanup).Returns(true);
    }

    [Fact]
    public async Task Execute_FeatureFlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.OrganizationEventCleanup).Returns(false);
        var context = CreateContext();

        await _sut.Execute(context);

        await _cleanupRepository.DidNotReceiveWithAnyArgs().GetNextPendingAsync();
        await _eventRepository.DidNotReceiveWithAnyArgs().DeleteManyByOrganizationIdAsync(default, default);
    }

    [Fact]
    public async Task Execute_NoPendingCleanup_ReturnsEarly()
    {
        _cleanupRepository.GetNextPendingAsync().Returns((OrganizationEventCleanup?)null);
        var context = CreateContext();

        await _sut.Execute(context);

        await _cleanupRepository.DidNotReceiveWithAnyArgs().MarkStartedAsync(default);
        await _eventRepository.DidNotReceiveWithAnyArgs().DeleteManyByOrganizationIdAsync(default, default);
    }

    [Fact]
    public async Task Execute_DeletesRepeatedlyThenCompletes_WhenBatchReturnsZero()
    {
        var pending = CreatePending();
        _cleanupRepository.GetNextPendingAsync().Returns(pending);
        _eventRepository
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId, Arg.Any<int>())
            .Returns(2000, 2000, 500, 0);
        var context = CreateContext();

        await _sut.Execute(context);

        await _cleanupRepository.Received(1).MarkStartedAsync(pending.Id);
        await _eventRepository.Received(4)
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId, Arg.Any<int>());
        await _cleanupRepository.Received(2).IncrementProgressAsync(pending.Id, 2000);
        await _cleanupRepository.Received(1).IncrementProgressAsync(pending.Id, 500);
        await _cleanupRepository.Received(1).MarkCompletedAsync(pending.Id);
        await _cleanupRepository.DidNotReceiveWithAnyArgs().RecordErrorAsync(default, default!);
    }

    [Fact]
    public async Task Execute_CancellationRequested_LeavesPending()
    {
        var pending = CreatePending();
        _cleanupRepository.GetNextPendingAsync().Returns(pending);

        using var cts = new CancellationTokenSource();
        _eventRepository
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId, Arg.Any<int>())
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromResult(2000);
            });
        var context = CreateContext(cts.Token);

        await _sut.Execute(context);

        await _cleanupRepository.Received(1).MarkStartedAsync(pending.Id);
        await _cleanupRepository.Received(1).IncrementProgressAsync(pending.Id, 2000);
        await _cleanupRepository.DidNotReceive().MarkCompletedAsync(pending.Id);
    }

    [Fact]
    public async Task Execute_DeleteThrows_RecordsErrorAndDoesNotComplete()
    {
        var pending = CreatePending();
        _cleanupRepository.GetNextPendingAsync().Returns(pending);
        _eventRepository
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId, Arg.Any<int>())
            .Throws(new InvalidOperationException("boom"));
        var context = CreateContext();

        // BaseJob.Execute swallows exceptions after logging; we verify via substitute calls.
        await _sut.Execute(context);

        await _cleanupRepository.Received(1).MarkStartedAsync(pending.Id);
        await _cleanupRepository.Received(1).RecordErrorAsync(pending.Id, "boom");
        await _cleanupRepository.DidNotReceive().MarkCompletedAsync(pending.Id);
    }

    private static OrganizationEventCleanup CreatePending() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        QueuedAt = DateTime.UtcNow,
    };

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }
}
