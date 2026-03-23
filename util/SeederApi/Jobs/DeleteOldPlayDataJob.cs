// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core;
using Bit.Core.Jobs;
using Bit.SeederApi.Commands.Interfaces;
using Bit.SeederApi.Queries.Interfaces;
using Quartz;

namespace Bit.SeederApi.Jobs;

public class DeleteOldPlayDataJob : BaseJob
{
    private readonly IGetAllPlayIdsQuery _getAllPlayIdsQuery;
    private readonly IDestroyBatchScenesCommand _destroyBatchScenesCommand;

    public DeleteOldPlayDataJob(
        IGetAllPlayIdsQuery getAllPlayIdsQuery,
        IDestroyBatchScenesCommand destroyBatchScenesCommand,
        ILogger<DeleteOldPlayDataJob> logger)
        : base(logger)
    {
        _getAllPlayIdsQuery = getAllPlayIdsQuery;
        _destroyBatchScenesCommand = destroyBatchScenesCommand;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteOldPlayDataJob");
        var olderThan = DateTime.UtcNow.AddDays(-1);
        var playIds = _getAllPlayIdsQuery.GetAllPlayIds(olderThan);
        await _destroyBatchScenesCommand.DestroyAsync(playIds);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DeleteOldPlayDataJob. Deleted {playIdCount} root items", playIds.Count);
    }
}
