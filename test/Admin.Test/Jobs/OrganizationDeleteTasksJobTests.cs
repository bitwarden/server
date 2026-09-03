using Bit.Admin.Jobs;
using Bit.Core;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace Admin.Test.Jobs;

public class OrganizationDeleteTasksJobTests
{
    private readonly IOrganizationDeleteTaskRepository _cleanupRepository;
    private readonly IOrganizationDeleteTaskHandler _handler;
    private readonly IFeatureService _featureService;
    private readonly ILogger<OrganizationDeleteTasksJob> _logger;
    private readonly OrganizationDeleteTasksJob _sut;

    public OrganizationDeleteTasksJobTests()
    {
        _cleanupRepository = Substitute.For<IOrganizationDeleteTaskRepository>();
        _handler = Substitute.For<IOrganizationDeleteTaskHandler>();
        _handler.TaskType.Returns(OrganizationDeleteTaskType.EventsCleanup);
        _featureService = Substitute.For<IFeatureService>();
        _logger = Substitute.For<ILogger<OrganizationDeleteTasksJob>>();
        _sut = new OrganizationDeleteTasksJob(
            _cleanupRepository,
            new[] { _handler },
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

        await _cleanupRepository.DidNotReceiveWithAnyArgs().ClaimNextPendingAsync();
        await _handler.DidNotReceiveWithAnyArgs().DeleteBatchAsync(default!, default);
    }

    [Fact]
    public async Task Execute_NoPendingCleanup_ReturnsEarly()
    {
        QueuePending();
        var context = CreateContext();

        await _sut.Execute(context);

        await _handler.DidNotReceiveWithAnyArgs().DeleteBatchAsync(default!, default);
    }

    [Fact]
    public async Task Execute_NoHandlerForTaskType_LeavesForRetryWithoutRecordingFailure()
    {
        // SUT built with no handlers, so the claimed task's type has no dispatch target.
        var sut = new OrganizationDeleteTasksJob(
            _cleanupRepository,
            Array.Empty<IOrganizationDeleteTaskHandler>(),
            _featureService,
            _logger);
        var pending = CreatePending();
        QueuePending(pending);
        var context = CreateContext();

        await sut.Execute(context);

        // Deliberately does not burn the retry budget; the task is left for stale-lease reclaim.
        await _cleanupRepository.DidNotReceiveWithAnyArgs().UpdateErrorAsync(default, default!);
        await _cleanupRepository.DidNotReceiveWithAnyArgs().UpdateCompletedAsync(default);
        await _cleanupRepository.DidNotReceiveWithAnyArgs().UpdateProgressAsync(default, default);
    }

    [Fact]
    public async Task Execute_NoHandlerForTaskType_RecentlyCreated_LogsWarningNotError()
    {
        var sut = new OrganizationDeleteTasksJob(
            _cleanupRepository,
            Array.Empty<IOrganizationDeleteTaskHandler>(),
            _featureService,
            _logger);
        // Freshly enqueued: a missing handler is plausibly just rolling-deploy skew.
        var pending = CreatePending();
        pending.CreationDate = DateTime.UtcNow;
        QueuePending(pending);
        var context = CreateContext();

        await sut.Execute(context);

        AssertLogged(LogLevel.Warning);
        AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public async Task Execute_NoHandlerForTaskType_UnhandledPastThreshold_EscalatesToError()
    {
        var sut = new OrganizationDeleteTasksJob(
            _cleanupRepository,
            Array.Empty<IOrganizationDeleteTaskHandler>(),
            _featureService,
            _logger);
        // Unhandled for hours: deploy skew is no longer a plausible explanation, so escalate.
        var pending = CreatePending();
        pending.CreationDate = DateTime.UtcNow.AddHours(-2);
        QueuePending(pending);
        var context = CreateContext();

        await sut.Execute(context);

        AssertLogged(LogLevel.Error);
        AssertNotLogged(LogLevel.Warning);
    }

    [Fact]
    public async Task Execute_DeletesRepeatedlyThenCompletes_WhenBatchReturnsZero()
    {
        var pending = CreatePending();
        QueuePending(pending);
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Returns(2000, 2000, 500, 0);
        var context = CreateContext();

        await _sut.Execute(context);

        await _handler.Received(4)
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>());
        await _cleanupRepository.Received(2).UpdateProgressAsync(pending.Id, 2000);
        await _cleanupRepository.Received(1).UpdateProgressAsync(pending.Id, 500);
        await _cleanupRepository.Received(1).UpdateCompletedAsync(pending.Id);
        await _cleanupRepository.DidNotReceiveWithAnyArgs().UpdateErrorAsync(default, default!);
    }

    [Fact]
    public async Task Execute_MultiplePendingTasks_DrainsAllInOneRun()
    {
        // Regression: the job used to claim exactly one task per firing.
        var first = CreatePending();
        var second = CreatePending();
        var third = CreatePending();
        QueuePending(first, second, third);
        _handler.DeleteBatchAsync(Arg.Any<OrganizationDeleteTask>(), Arg.Any<CancellationToken>())
            .Returns(0);

        await _sut.Execute(CreateContext());

        await _cleanupRepository.Received(1).UpdateCompletedAsync(first.Id);
        await _cleanupRepository.Received(1).UpdateCompletedAsync(second.Id);
        await _cleanupRepository.Received(1).UpdateCompletedAsync(third.Id);
        // Three claims plus the one that returns null and ends the run.
        await _cleanupRepository.Received(4).ClaimNextPendingAsync();
    }

    [Fact]
    public async Task Execute_QueueEmpty_ClaimsOnceAndStops()
    {
        QueuePending();

        await _sut.Execute(CreateContext());

        await _cleanupRepository.Received(1).ClaimNextPendingAsync();
    }

    [Fact]
    public async Task Execute_TaskFails_StillDrainsTasksBehindIt()
    {
        // The queue is strictly ordered, so aborting on one failure would stall everything behind it.
        var failing = CreatePending();
        var following = CreatePending();
        QueuePending(failing, following);
        _handler.DeleteBatchAsync(failing, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));
        _handler.DeleteBatchAsync(following, Arg.Any<CancellationToken>())
            .Returns(0);

        await _sut.Execute(CreateContext());

        await _cleanupRepository.Received(1).UpdateErrorAsync(failing.Id, Arg.Any<string>());
        await _cleanupRepository.DidNotReceive().UpdateCompletedAsync(failing.Id);
        await _cleanupRepository.Received(1).UpdateCompletedAsync(following.Id);
    }

    [Fact]
    public async Task Execute_TaskTypeHasNoHandler_StillDrainsTasksBehindIt()
    {
        var unhandled = CreatePending();
        unhandled.TaskType = (OrganizationDeleteTaskType)99;
        var following = CreatePending();
        QueuePending(unhandled, following);
        _handler.DeleteBatchAsync(following, Arg.Any<CancellationToken>())
            .Returns(0);

        await _sut.Execute(CreateContext());

        await _cleanupRepository.DidNotReceiveWithAnyArgs().UpdateErrorAsync(default, default!);
        await _cleanupRepository.Received(1).UpdateCompletedAsync(following.Id);
    }

    [Fact]
    public async Task Execute_CancellationRequested_StopsClaimingFurtherTasks()
    {
        var first = CreatePending();
        var second = CreatePending();
        QueuePending(first, second);

        using var cts = new CancellationTokenSource();
        _handler.DeleteBatchAsync(first, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromResult(0);
            });

        await _sut.Execute(CreateContext(cts.Token));

        // Only the first claim happens; the loop checks cancellation before claiming again.
        await _cleanupRepository.Received(1).ClaimNextPendingAsync();
        await _handler.DidNotReceive().DeleteBatchAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_CancellationRequested_LeavesPending()
    {
        var pending = CreatePending();
        QueuePending(pending);

        using var cts = new CancellationTokenSource();
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromResult(2000);
            });
        var context = CreateContext(cts.Token);

        await _sut.Execute(context);

        await _cleanupRepository.Received(1).UpdateProgressAsync(pending.Id, 2000);
        await _cleanupRepository.DidNotReceive().UpdateCompletedAsync(pending.Id);
    }

    [Fact]
    public async Task Execute_CancellationAlreadySignalled_DoesNotCompleteWithoutDeleting()
    {
        var pending = CreatePending();
        QueuePending(pending);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await _sut.Execute(CreateContext(cts.Token));

        // Nothing was purged, so completing here would strand the organization's events.
        await _handler.DidNotReceiveWithAnyArgs().DeleteBatchAsync(default!, default);
        await _cleanupRepository.DidNotReceive().UpdateCompletedAsync(pending.Id);
    }

    [Fact]
    public async Task Execute_DeleteThrows_RecordsErrorAndDoesNotComplete()
    {
        var pending = CreatePending();
        QueuePending(pending);
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));
        var context = CreateContext();

        await _sut.Execute(context);

        // The exception type is recorded, never the message.
        await _cleanupRepository.Received(1).UpdateErrorAsync(pending.Id, typeof(InvalidOperationException).FullName!);
        await _cleanupRepository.DidNotReceive().UpdateCompletedAsync(pending.Id);
    }

    [Fact]
    public async Task Execute_DeleteThrows_BelowFailureCap_DoesNotLogAbandonment()
    {
        var pending = CreatePending();
        QueuePending(pending);
        _cleanupRepository.UpdateErrorAsync(pending.Id, Arg.Any<string>())
            .Returns(OrganizationDeleteTask.MaxFailureCount - 1);
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        await _sut.Execute(CreateContext());

        // The task will be reclaimed once its lease goes stale, so this is not an abandonment.
        AssertNotLoggedContaining(LogLevel.Error, "Abandoning");
    }

    [Fact]
    public async Task Execute_DeleteThrows_AtFailureCap_LogsAbandonment()
    {
        var pending = CreatePending();
        QueuePending(pending);
        _cleanupRepository.UpdateErrorAsync(pending.Id, Arg.Any<string>())
            .Returns(OrganizationDeleteTask.MaxFailureCount);
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        await _sut.Execute(CreateContext());

        // Past the cap the task is never claimed again, so the GDPR cleanup has silently stopped.
        AssertLoggedContaining(LogLevel.Error, "Abandoning");
    }

    [Fact]
    public async Task Execute_DeleteThrows_DoesNotLeakRowKeyIdentifiersInError()
    {
        var pending = CreatePending();
        QueuePending(pending);
        // Azure SDK messages can embed row-key identifiers; these must never be persisted.
        var leakyMessage = "The specified entity already exists. UserId=abc123, CipherId=def456";
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException(leakyMessage));
        var context = CreateContext();

        await _sut.Execute(context);

        await _cleanupRepository.Received(1).UpdateErrorAsync(
            pending.Id,
            Arg.Is<string>(error => !error.Contains("UserId") && !error.Contains("CipherId")));
    }

    [Fact]
    public async Task Execute_DeleteThrows_DoesNotLeakRowKeyIdentifiersInLogs()
    {
        var pending = CreatePending();
        QueuePending(pending);
        // Failures are not rethrown, so this log is the only thing keeping the Azure SDK
        // message out of the logging pipeline.
        var leakyMessage = "The specified entity already exists. UserId=abc123, CipherId=def456";
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException(leakyMessage));

        await _sut.Execute(CreateContext());

        AssertNotLoggedContaining(LogLevel.Error, "UserId");
        AssertNotLoggedContaining(LogLevel.Error, "CipherId");
        AssertLoggedContaining(LogLevel.Error, typeof(InvalidOperationException).FullName!);
    }

    /// <summary>
    /// Matches a log entry by content, for levels where more than one entry can be emitted.
    /// </summary>
    private void AssertLoggedContaining(LogLevel level, string fragment) =>
        _logger.Received(1).Log(
            level,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(fragment)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

    private void AssertNotLoggedContaining(LogLevel level, string fragment) =>
        _logger.DidNotReceive().Log(
            level,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(fragment)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

    private void AssertLogged(LogLevel level) =>
        _logger.Received(1).Log(
            level,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

    private void AssertNotLogged(LogLevel level) =>
        _logger.DidNotReceive().Log(
            level,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

    /// <summary>
    /// Stubs the queue with the given tasks in order, then empty. The job claims in a loop until
    /// it gets null, so a stub returning the same task forever would spin for the whole run budget.
    /// </summary>
    private void QueuePending(params OrganizationDeleteTask[] tasks)
    {
        var queue = new Queue<OrganizationDeleteTask>(tasks);
        _cleanupRepository.ClaimNextPendingAsync()
            .Returns(_ => queue.Count > 0 ? queue.Dequeue() : null);
    }

    private static OrganizationDeleteTask CreatePending() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        TaskType = OrganizationDeleteTaskType.EventsCleanup,
        CreationDate = DateTime.UtcNow,
    };

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }
}
