using Bit.Core.Billing.Services;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Api.Jobs;

public class ValidateUsersJob : BaseJob
{
    private readonly ILicensingService _licensingService;

    public ValidateUsersJob(
        ILicensingService licensingService,
        ILogger<ValidateUsersJob> logger)
        : base(logger)
    {
        _licensingService = licensingService;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        await _licensingService.ValidateUsersAsync();
    }
}
