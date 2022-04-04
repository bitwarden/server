using System;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Repositories;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Admin.Jobs
{
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
            await _maintenanceRepository.DeleteExpiredSponsorshipsAsync();
            _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DeleteExpiredSponsorshipsAsync");
        }
    }
}
