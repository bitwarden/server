using System;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Admin.Jobs
{
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
            await _maintenanceRepository.RebuildIndexesAsync();
        }
    }
}
