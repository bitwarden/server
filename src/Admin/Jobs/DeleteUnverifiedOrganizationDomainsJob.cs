using Bit.Core.Jobs;
using Quartz;

namespace Bit.Admin.Jobs;

public class DeleteUnverifiedOrganizationDomainsJob : BaseJob
{
    public DeleteUnverifiedOrganizationDomainsJob(ILogger logger) : base(logger)
    {
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        //Get domains that have not been verified within 72 hours
        //Send email to administrators
        //Update table with email sent

        //check domains that have not been verified within 7 days 
        //delete domains
        
        //end
    }
}
