using Bit.Admin.Auth.Jobs;
using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Quartz;

namespace Bit.Admin.Tools.Jobs;

// A run loops until the backlog drains and can span many 5-minute trigger intervals. Send_DeleteMany
// doesn't report rows-affected, so an overlapping run re-fetching the same head-of-queue batch would
// have its DELETE affect zero rows while DeleteManySendsAsync still reports every id as deleted —
// duplicate Send_Deleted_* events and push notifications for Sends the other run already handled.
[DisallowConcurrentExecution]
public class DeleteSendsJob : BaseJob
{
    private const int BatchSize = 2000;
    private static readonly TimeSpan _interBatchDelay = TimeSpan.FromMilliseconds(250);

    private readonly ISendRepository _sendRepository;
    private readonly IServiceProvider _serviceProvider;

    public DeleteSendsJob(
        ISendRepository sendRepository,
        IServiceProvider serviceProvider,
        ILogger<DatabaseExpiredGrantsJob> logger)
        : base(logger)
    {
        _sendRepository = sendRepository;
        _serviceProvider = serviceProvider;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        var totalDeleted = 0;

        // Skipped Sends (e.g. their blob delete failed) stay at the head of the DeletionDate-ordered
        // queue and are re-fetched every iteration. Tracking distinct skipped ids bounds how much of
        // a run can be wasted re-retrying the same stuck rows: once this set reaches BatchSize, every
        // row in the next fetch is guaranteed to already be a known-stuck one.
        var skippedIds = new HashSet<Guid>();

        using var scope = _serviceProvider.CreateScope();
        var nonAnonymousSendCommand = scope.ServiceProvider.GetRequiredService<INonAnonymousSendCommand>();

        while (!context.CancellationToken.IsCancellationRequested)
        {
            var sends = await _sendRepository.GetManyByDeletionDateAsync(DateTime.UtcNow, BatchSize);
            if (sends.Count == 0)
            {
                break;
            }

            var deletedIds = await nonAnonymousSendCommand.DeleteManySendsAsync(sends);
            totalDeleted += deletedIds.Count;
            skippedIds.UnionWith(sends.Select(s => s.Id).Except(deletedIds));

            if (deletedIds.Count == 0 || skippedIds.Count >= BatchSize)
            {
                // Either every Send in this batch was skipped, or enough distinct Sends have been
                // skipped across this run that the next fetch can only return already-known-stuck
                // rows. Stop instead of re-reading and re-retrying them.
                _logger.LogWarning(Constants.BypassFiltersEventId,
                    "Stopping after {0} skipped sends this run; the next batch would only re-read stuck rows.", skippedIds.Count);
                break;
            }

            if (sends.Count < BatchSize)
            {
                // Fewer rows than requested means the backlog is drained; no need to delay after it.
                break;
            }

            await Task.Delay(_interBatchDelay);
        }

        _logger.LogInformation(Constants.BypassFiltersEventId, "Deleted {0} sends.", totalDeleted);
    }
}
