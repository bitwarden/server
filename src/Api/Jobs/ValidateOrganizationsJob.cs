using Bit.Core.Billing.Services;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Api.Jobs;

public class ValidateOrganizationsJob : BaseJob
{
    private readonly ILicensingService _licensingService;

    public ValidateOrganizationsJob(
        ILicensingService licensingService,
        ILogger<ValidateOrganizationsJob> logger)
        : base(logger)
    {
        _licensingService = licensingService;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        await _licensingService.ValidateOrganizationsAsync();
    }
}
