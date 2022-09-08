using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Quartz;

namespace Bit.Admin.Jobs;

public class DatabaseRebuildlIndexesJob : BaseJob
{
    private readonly IMaintenanceRepository _maintenanceRepository;

    public DatabaseRebuildlIndexesJob(
        IMaintenanceRepository maintenanceRepository,
        ILogger<DatabaseRebuildlIndexesJob> logger)
        : base(logger)
    {
        _maintenanceRepository = maintenanceRepository;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: RebuildIndexesAsync");
        await _maintenanceRepository.RebuildIndexesAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: RebuildIndexesAsync");
    }
}
