using System;
using System.Threading.Tasks;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class ValidateOrganizationsJob : IJob
    {
        private readonly ILicensingService _licensingService;
        private readonly ILogger<ValidateOrganizationsJob> _logger;

        public ValidateOrganizationsJob(
            ILicensingService licensingService,
            ILogger<ValidateOrganizationsJob> logger)
        {
            _licensingService = licensingService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _licensingService.ValidateOrganizationsAsync();
            }
            catch(Exception e)
            {
                _logger.LogError(2, e, "Error performing {0}.", nameof(ValidateOrganizationsJob));
            }
        }
    }
}
