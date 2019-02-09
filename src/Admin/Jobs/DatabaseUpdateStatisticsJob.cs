using System;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Admin.Jobs
{
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
            await _maintenanceRepository.UpdateStatisticsAsync();
            await _maintenanceRepository.DisableCipherAutoStatsAsync();
        }
    }
}
