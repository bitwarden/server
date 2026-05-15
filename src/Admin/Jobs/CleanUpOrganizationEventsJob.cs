#nullable enable

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

    private readonly IOrganizationEventCleanupRepository _cleanupRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IFeatureService _featureService;

    public CleanUpOrganizationEventsJob(
        IOrganizationEventCleanupRepository cleanupRepository,
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

        var pending = await _cleanupRepository.GetNextPendingAsync();
        if (pending is null)
        {
            return;
        }

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Starting event cleanup for organization {OrganizationId} (cleanup {CleanupId})",
            pending.OrganizationId, pending.Id);

        await _cleanupRepository.MarkStartedAsync(pending.Id);

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

                await _cleanupRepository.IncrementProgressAsync(pending.Id, deleted);
                totalDeleted += deleted;
            }

            if (deleted == 0)
            {
                await _cleanupRepository.MarkCompletedAsync(pending.Id);
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
            var message = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            await _cleanupRepository.RecordErrorAsync(pending.Id, message);
            throw;
        }
    }
}
