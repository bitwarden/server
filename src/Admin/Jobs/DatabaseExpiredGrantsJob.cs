using System;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Admin.Jobs
{
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
            await _maintenanceRepository.DeleteExpiredGrantsAsync();
        }
    }
}
