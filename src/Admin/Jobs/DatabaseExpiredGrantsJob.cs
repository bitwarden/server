using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Quartz;

namespace Bit.Admin.Jobs;

public class DatabaseExpiredGrantsJob : BaseJob
{
    private readonly IMaintenanceRepository _maintenanceRepository;

    public DatabaseExpiredGrantsJob(
        IMaintenanceRepository maintenanceRepository,
        ILogger<DatabaseExpiredGrantsJob> logger)
        : base(logger)
    {
        _maintenanceRepository = maintenanceRepository;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteExpiredGrantsAsync");
        await _maintenanceRepository.DeleteExpiredGrantsAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DeleteExpiredGrantsAsync");
    }
}
