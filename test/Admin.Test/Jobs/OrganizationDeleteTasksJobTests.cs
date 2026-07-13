using Bit.Admin.Jobs;
using Bit.Core;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Services;
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
        _cleanupRepository.ClaimNextPendingAsync().Returns((OrganizationDeleteTask?)null);
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
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);
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
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);
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
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);
        var context = CreateContext();

        await sut.Execute(context);

        AssertLogged(LogLevel.Error);
        AssertNotLogged(LogLevel.Warning);
    }

    [Fact]
    public async Task Execute_DeletesRepeatedlyThenCompletes_WhenBatchReturnsZero()
    {
        var pending = CreatePending();
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);
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
    public async Task Execute_CancellationRequested_LeavesPending()
    {
        var pending = CreatePending();
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);

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
    public async Task Execute_DeleteThrows_RecordsErrorAndDoesNotComplete()
    {
        var pending = CreatePending();
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);
        _handler
            .DeleteBatchAsync(pending, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));
        var context = CreateContext();

        // BaseJob.Execute swallows exceptions after logging; we verify via substitute calls.
        await _sut.Execute(context);

        // The exception type is recorded, never the message.
        await _cleanupRepository.Received(1).UpdateErrorAsync(pending.Id, typeof(InvalidOperationException).FullName!);
        await _cleanupRepository.DidNotReceive().UpdateCompletedAsync(pending.Id);
    }

    [Fact]
    public async Task Execute_DeleteThrows_DoesNotLeakRowKeyIdentifiersInError()
    {
        var pending = CreatePending();
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);
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
