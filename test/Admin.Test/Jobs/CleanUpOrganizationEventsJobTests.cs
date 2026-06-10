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
    private readonly IOrganizationDeleteTaskRepository _cleanupRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IFeatureService _featureService;
    private readonly ILogger<CleanUpOrganizationEventsJob> _logger;
    private readonly CleanUpOrganizationEventsJob _sut;

    public CleanUpOrganizationEventsJobTests()
    {
        _cleanupRepository = Substitute.For<IOrganizationDeleteTaskRepository>();
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

        await _cleanupRepository.DidNotReceiveWithAnyArgs().ClaimNextPendingAsync();
        await _eventRepository.DidNotReceiveWithAnyArgs().DeleteManyByOrganizationIdAsync(default);
    }

    [Fact]
    public async Task Execute_NoPendingCleanup_ReturnsEarly()
    {
        _cleanupRepository.ClaimNextPendingAsync().Returns((OrganizationDeleteTask?)null);
        var context = CreateContext();

        await _sut.Execute(context);

        await _eventRepository.DidNotReceiveWithAnyArgs().DeleteManyByOrganizationIdAsync(default);
    }

    [Fact]
    public async Task Execute_DeletesRepeatedlyThenCompletes_WhenBatchReturnsZero()
    {
        var pending = CreatePending();
        _cleanupRepository.ClaimNextPendingAsync().Returns(pending);
        _eventRepository
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId)
            .Returns(2000, 2000, 500, 0);
        var context = CreateContext();

        await _sut.Execute(context);

        await _eventRepository.Received(4)
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId);
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
        _eventRepository
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId)
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
        _eventRepository
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId)
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
        _eventRepository
            .DeleteManyByOrganizationIdAsync(pending.OrganizationId)
            .Throws(new InvalidOperationException(leakyMessage));
        var context = CreateContext();

        await _sut.Execute(context);

        await _cleanupRepository.Received(1).UpdateErrorAsync(
            pending.Id,
            Arg.Is<string>(error => !error.Contains("UserId") && !error.Contains("CipherId")));
    }

    private static OrganizationDeleteTask CreatePending() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        CreationDate = DateTime.UtcNow,
    };

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }
}
