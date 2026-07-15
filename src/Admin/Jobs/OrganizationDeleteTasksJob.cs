#nullable enable

using Azure;
using Bit.Core;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Admin.Jobs;

/// <summary>
/// Drains the <c>OrganizationDeleteTask</c> queue: claims the next pending task and dispatches it to
/// the <see cref="IOrganizationDeleteTaskHandler"/> registered for its type, deleting in bounded
/// batches within a run budget so a large cleanup resumes across runs. All lease, progress, and
/// error bookkeeping lives here; handlers only implement the per-type batch delete.
/// </summary>
public class OrganizationDeleteTasksJob : BaseJob
{
    private static readonly TimeSpan _runBudget = TimeSpan.FromMinutes(4);

    // A missing handler is expected transiently while a rolling deploy is in flight, but a
    // genuinely orphaned type (enqueued with no handler that will ever be deployed) stays
    // unhandled long after any deploy completes. Below this age we log the miss at Warning;
    // beyond it we escalate to Error so a stuck type can be alerted on rather than lost in
    // steady Warning volume.
    private static readonly TimeSpan _orphanedTaskEscalationThreshold = TimeSpan.FromHours(1);

    private readonly IOrganizationDeleteTaskRepository _cleanupRepository;
    private readonly IReadOnlyDictionary<OrganizationDeleteTaskType, IOrganizationDeleteTaskHandler> _handlers;
    private readonly IFeatureService _featureService;

    public OrganizationDeleteTasksJob(
        IOrganizationDeleteTaskRepository cleanupRepository,
        IEnumerable<IOrganizationDeleteTaskHandler> handlers,
        IFeatureService featureService,
        ILogger<OrganizationDeleteTasksJob> logger)
        : base(logger)
    {
        _cleanupRepository = cleanupRepository;
        // Throws at construction if two handlers claim the same type, failing fast on misconfiguration.
        _handlers = handlers.ToDictionary(handler => handler.TaskType);
        _featureService = featureService;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.OrganizationEventCleanup))
        {
            return;
        }

        var pending = await _cleanupRepository.ClaimNextPendingAsync();
        if (pending is null)
        {
            return;
        }

        if (!_handlers.TryGetValue(pending.TaskType, out var handler))
        {
            // No handler is registered for this type. This is expected transiently when the server
            // enqueuing tasks is ahead of this worker during a rolling deploy. We deliberately do NOT
            // record a failure: doing so would burn the retry budget and could permanently abandon a
            // task that only needs a newer worker. The claim lease expires and the task is reclaimed
            // on a later run. Escalate to Error once it has been unhandled long enough that deploy
            // skew is no longer a plausible explanation.
            var unhandledFor = DateTime.UtcNow - pending.CreationDate;
            var logLevel = unhandledFor >= _orphanedTaskEscalationThreshold ? LogLevel.Error : LogLevel.Warning;
            _logger.Log(logLevel, Constants.BypassFiltersEventId,
                "No handler registered for organization delete task type {TaskType} (task {TaskId}); leaving for retry. Unhandled for {UnhandledMinutes:N0} minutes.",
                pending.TaskType, pending.Id, unhandledFor.TotalMinutes);
            return;
        }

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Starting {TaskType} cleanup for organization {OrganizationId} (task {TaskId})",
            pending.TaskType, pending.OrganizationId, pending.Id);

        var deadline = DateTime.UtcNow.Add(_runBudget);
        var deleted = 0;
        var totalDeleted = 0L;

        try
        {
            while (DateTime.UtcNow < deadline && !context.CancellationToken.IsCancellationRequested)
            {
                deleted = await handler.DeleteBatchAsync(pending, context.CancellationToken);
                if (deleted == 0)
                {
                    break;
                }

                await _cleanupRepository.UpdateProgressAsync(pending.Id, deleted);
                totalDeleted += deleted;
            }

            if (deleted == 0)
            {
                await _cleanupRepository.UpdateCompletedAsync(pending.Id);
                _logger.LogInformation(Constants.BypassFiltersEventId,
                    "Completed {TaskType} cleanup for organization {OrganizationId}; deleted {Deleted} items this run",
                    pending.TaskType, pending.OrganizationId, totalDeleted);
            }
            else
            {
                _logger.LogInformation(Constants.BypassFiltersEventId,
                    "Paused {TaskType} cleanup for organization {OrganizationId}; deleted {Deleted} items this run, will resume",
                    pending.TaskType, pending.OrganizationId, totalDeleted);
            }
        }
        catch (Exception ex)
        {
            // Store a sanitized error, never ex.Message: Azure SDK messages can embed
            // row-key identifiers (e.g. UserId=..., CipherId=...) that must not be persisted.
            await _cleanupRepository.UpdateErrorAsync(pending.Id, BuildSanitizedError(ex));
            throw;
        }
    }

    private static string BuildSanitizedError(Exception ex) => ex switch
    {
        RequestFailedException rfe => $"{nameof(RequestFailedException)} (Status: {rfe.Status}, ErrorCode: {rfe.ErrorCode})",
        _ => ex.GetType().FullName ?? ex.GetType().Name,
    };
}
