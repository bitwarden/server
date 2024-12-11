using Bit.Core;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Admin.Jobs;

public class DeleteUnverifiedOrganizationDomainsJob : BaseJob
{
    private readonly IServiceProvider _serviceProvider;

    public DeleteUnverifiedOrganizationDomainsJob(
        IServiceProvider serviceProvider,
        ILogger<DeleteUnverifiedOrganizationDomainsJob> logger
    )
        : base(logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            "Execute job task: DeleteUnverifiedOrganizationDomainsJob: Start"
        );
        using (var serviceScope = _serviceProvider.CreateScope())
        {
            var organizationDomainService =
                serviceScope.ServiceProvider.GetRequiredService<IOrganizationDomainService>();
            await organizationDomainService.OrganizationDomainMaintenanceAsync();
        }
        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            "Execute job task: DeleteUnverifiedOrganizationDomainsJob: End"
        );
    }
}
