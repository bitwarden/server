using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Api.Jobs;

public class ValidateUsersJob : BaseJob
{
    private readonly ILicensingService _licensingService;

    public ValidateUsersJob(ILicensingService licensingService, ILogger<ValidateUsersJob> logger)
        : base(logger)
    {
        _licensingService = licensingService;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        await _licensingService.ValidateUsersAsync();
    }
}
