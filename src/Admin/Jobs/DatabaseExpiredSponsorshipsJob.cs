using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Admin.Jobs;

public class DatabaseExpiredSponsorshipsJob : BaseJob
{
    private GlobalSettings _globalSettings;
    private readonly IMaintenanceRepository _maintenanceRepository;

    public DatabaseExpiredSponsorshipsJob(
        IMaintenanceRepository maintenanceRepository,
        ILogger<DatabaseExpiredSponsorshipsJob> logger,
        GlobalSettings globalSettings)
        : base(logger)
    {
        _maintenanceRepository = maintenanceRepository;
        _globalSettings = globalSettings;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (_globalSettings.SelfHosted && !_globalSettings.EnableCloudCommunication)
        {
            return;
        }
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteExpiredSponsorshipsAsync");

        // allow a 90 day grace period before deleting
        var deleteDate = DateTime.UtcNow.AddDays(-90);

        await _maintenanceRepository.DeleteExpiredSponsorshipsAsync(deleteDate);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DeleteExpiredSponsorshipsAsync");
    }
}
