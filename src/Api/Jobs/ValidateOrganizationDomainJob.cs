using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Api.Jobs;

public class ValidateOrganizationDomainJob : BaseJob
{
    private readonly IVerificationDomainService _verificationDomainService;

    public ValidateOrganizationDomainJob(
        IVerificationDomainService verificationDomainService,
        ILogger<ValidateOrganizationDomainJob> logger)
        : base(logger)
    {
        _verificationDomainService = verificationDomainService;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: ValidateOrganizationDomainJob: Start");
        await _verificationDomainService.ValidateOrganizationsDomainAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: ValidateOrganizationDomainJob: End");
    }
}
