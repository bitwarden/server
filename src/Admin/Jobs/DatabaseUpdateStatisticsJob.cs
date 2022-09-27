using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Quartz;

namespace Bit.Admin.Jobs;

public class DatabaseUpdateStatisticsJob : BaseJob
{
    private readonly IMaintenanceRepository _maintenanceRepository;

    public DatabaseUpdateStatisticsJob(
        IMaintenanceRepository maintenanceRepository,
        ILogger<DatabaseUpdateStatisticsJob> logger)
        : base(logger)
    {
        _maintenanceRepository = maintenanceRepository;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: UpdateStatisticsAsync");
        await _maintenanceRepository.UpdateStatisticsAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: UpdateStatisticsAsync");
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DisableCipherAutoStatsAsync");
        await _maintenanceRepository.DisableCipherAutoStatsAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DisableCipherAutoStatsAsync");
    }
}
