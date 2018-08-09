using System;
using System.Threading.Tasks;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class ValidateUsersJob : IJob
    {
        private readonly ILicensingService _licensingService;
        private readonly ILogger<ValidateUsersJob> _logger;

        public ValidateUsersJob(
            ILicensingService licensingService,
            ILogger<ValidateUsersJob> logger)
        {
            _licensingService = licensingService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _licensingService.ValidateUsersAsync();
            }
            catch(Exception e)
            {
                _logger.LogError(2, e, "Error performing {0}.", nameof(ValidateUsersJob));
            }
        }
    }
}
