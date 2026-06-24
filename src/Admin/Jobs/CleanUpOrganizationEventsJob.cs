#nullable enable

using Azure;
using Bit.Core;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Quartz;

namespace Bit.Admin.Jobs;

public class CleanUpOrganizationEventsJob : BaseJob
{
    private static readonly TimeSpan _runBudget = TimeSpan.FromMinutes(4);

    private readonly IOrganizationDeleteTaskRepository _cleanupRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IFeatureService _featureService;

    public CleanUpOrganizationEventsJob(
        IOrganizationDeleteTaskRepository cleanupRepository,
        IEventRepository eventRepository,
        IFeatureService featureService,
        ILogger<CleanUpOrganizationEventsJob> logger)
        : base(logger)
    {
        _cleanupRepository = cleanupRepository;
        _eventRepository = eventRepository;
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

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Starting event cleanup for organization {OrganizationId} (cleanup {CleanupId})",
            pending.OrganizationId, pending.Id);

        var deadline = DateTime.UtcNow.Add(_runBudget);
        var deleted = 0;
        var totalDeleted = 0L;

        try
        {
            while (DateTime.UtcNow < deadline && !context.CancellationToken.IsCancellationRequested)
            {
                deleted = await _eventRepository.DeleteManyByOrganizationIdAsync(
                    pending.OrganizationId);
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
                    "Completed event cleanup for organization {OrganizationId}; deleted {Deleted} events this run",
                    pending.OrganizationId, totalDeleted);
            }
            else
            {
                _logger.LogInformation(Constants.BypassFiltersEventId,
                    "Paused event cleanup for organization {OrganizationId}; deleted {Deleted} events this run, will resume",
                    pending.OrganizationId, totalDeleted);
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
