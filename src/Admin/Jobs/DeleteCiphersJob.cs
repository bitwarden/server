using System;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Admin.Jobs
{
    public class DeleteCiphersJob : BaseJob
    {
        private readonly ICipherRepository _cipherRepository;

        public DeleteCiphersJob(
            ICipherRepository cipherRepository,
            ILogger<DeleteCiphersJob> logger)
            : base(logger)
        {
            _cipherRepository = cipherRepository;
        }

        protected async override Task ExecuteJobAsync(IJobExecutionContext context)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteDeletedAsync");
            await _cipherRepository.DeleteDeletedAsync(DateTime.UtcNow.AddDays(-30));
            _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DeleteDeletedAsync");
        }
    }
}
