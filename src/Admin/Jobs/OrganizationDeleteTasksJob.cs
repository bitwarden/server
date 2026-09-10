#nullable enable

using Azure;
using Bit.Core;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Jobs;
using Bitwarden.Server.Sdk.Features;
using Quartz;

namespace Bit.Admin.Jobs;

/// <summary>
/// Drains the <c>OrganizationDeleteTask</c> queue, dispatching each claimed task to the
/// <see cref="IOrganizationDeleteTaskHandler"/> registered for its type. All lease, progress, and
/// error bookkeeping lives here; handlers only implement the per-type batch delete.
/// </summary>
public class OrganizationDeleteTasksJob : BaseJob
{
    // Budget for the whole run, shared across every task claimed in it. Under the trigger's
    // five-minute interval so the final batch lands before the next firing.
    private static readonly TimeSpan _runBudget = TimeSpan.FromMinutes(4);

    // Below this age a missing handler is plausibly rolling-deploy skew and logs at Warning; beyond
    // it the type is genuinely orphaned, so escalate to Error for alerting.
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

        var deadline = DateTime.UtcNow.Add(_runBudget);
        var tasksClaimed = 0;

        // Claiming one task per firing capped throughput at one organization per trigger interval
        // regardless of how little work each needed. Claiming a row stamps RevisionDate, which puts
        // it outside the claim predicate for the rest of this run, so the loop always progresses.
        while (DateTime.UtcNow < deadline && !context.CancellationToken.IsCancellationRequested)
        {
            var pending = await _cleanupRepository.ClaimNextPendingAsync();
            if (pending is null)
            {
                break;
            }

            tasksClaimed++;

            if (!_handlers.TryGetValue(pending.TaskType, out var handler))
            {
                LogMissingHandler(pending);
                continue;
            }

            await DrainTaskAsync(pending, handler, deadline, context.CancellationToken);
        }

        if (tasksClaimed > 0)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Organization delete tasks run claimed {TaskCount} task(s)", tasksClaimed);
        }
    }

    /// <summary>
    /// Purges one claimed task in bounded batches until it drains or <paramref name="deadline"/>
    /// passes. Failures are recorded rather than rethrown: the queue is strictly ordered, so
    /// aborting the run on one organization would stall every task behind it.
    /// </summary>
    private async Task DrainTaskAsync(
        OrganizationDeleteTask pending,
        IOrganizationDeleteTaskHandler handler,
        DateTime deadline,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Starting {TaskType} cleanup for organization {OrganizationId} (task {TaskId})",
            pending.TaskType, pending.OrganizationId, pending.Id);

        var drained = false;
        var totalDeleted = 0L;

        try
        {
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                var deleted = await handler.DeleteBatchAsync(pending, cancellationToken);
                if (deleted == 0)
                {
                    // An empty batch is the only proof there is nothing left to purge.
                    drained = true;
                    break;
                }

                await _cleanupRepository.UpdateProgressAsync(pending.Id, deleted);
                totalDeleted += deleted;
            }

            // Completing on anything but an empty batch would strand the organization's events: if
            // the budget was already spent or cancellation signalled, the loop above never ran.
            if (drained)
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
            var sanitizedError = BuildSanitizedError(ex);
            var failureCount = await _cleanupRepository.UpdateErrorAsync(pending.Id, sanitizedError);

            _logger.LogError(Constants.BypassFiltersEventId,
                "Failed {TaskType} cleanup for organization {OrganizationId} (task {TaskId}) with {Error}; failure {FailureCount} of {MaxFailureCount}.",
                pending.TaskType, pending.OrganizationId, pending.Id, sanitizedError, failureCount,
                OrganizationDeleteTask.MaxFailureCount);

            // Past the cap the task is never claimed again and goes quiet. Deleting these logs is a
            // GDPR obligation, so giving up gets its own signal for alerting.
            if (failureCount >= OrganizationDeleteTask.MaxFailureCount)
            {
                _logger.LogError(Constants.BypassFiltersEventId,
                    "Abandoning {TaskType} cleanup for organization {OrganizationId} (task {TaskId}) after {FailureCount} failures; it will not be retried.",
                    pending.TaskType, pending.OrganizationId, pending.Id, failureCount);
            }
        }
    }

    /// <summary>
    /// Logs a task whose type has no registered handler. Deliberately records no failure: that
    /// would burn the retry budget on a task that may only need a newer worker. The claim lease
    /// expires and the task is reclaimed on a later run.
    /// </summary>
    private void LogMissingHandler(OrganizationDeleteTask pending)
    {
        var unhandledFor = DateTime.UtcNow - pending.CreationDate;
        var logLevel = unhandledFor >= _orphanedTaskEscalationThreshold ? LogLevel.Error : LogLevel.Warning;
        _logger.Log(logLevel, Constants.BypassFiltersEventId,
            "No handler registered for organization delete task type {TaskType} (task {TaskId}); leaving for retry. Unhandled for {UnhandledMinutes:N0} minutes.",
            pending.TaskType, pending.Id, unhandledFor.TotalMinutes);
    }

    private static string BuildSanitizedError(Exception ex) => ex switch
    {
        RequestFailedException rfe => $"{nameof(RequestFailedException)} (Status: {rfe.Status}, ErrorCode: {rfe.ErrorCode})",
        _ => ex.GetType().FullName ?? ex.GetType().Name,
    };
}
