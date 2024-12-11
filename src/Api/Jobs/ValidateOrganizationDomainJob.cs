using Bit.Core;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Api.Jobs;

public class ValidateOrganizationDomainJob : BaseJob
{
    private readonly IServiceProvider _serviceProvider;

    public ValidateOrganizationDomainJob(
        IServiceProvider serviceProvider,
        ILogger<ValidateOrganizationDomainJob> logger
    )
        : base(logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            "Execute job task: ValidateOrganizationDomainJob: Start"
        );
        using (var serviceScope = _serviceProvider.CreateScope())
        {
            var organizationDomainService =
                serviceScope.ServiceProvider.GetRequiredService<IOrganizationDomainService>();
            await organizationDomainService.ValidateOrganizationsDomainAsync();
        }
        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            "Execute job task: ValidateOrganizationDomainJob: End"
        );
    }
}
