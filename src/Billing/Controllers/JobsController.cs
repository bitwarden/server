using Bit.Billing.Jobs;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Bit.Billing.Controllers;

[Route("jobs")]
[SelfHosted(NotSelfHostedOnly = true)]
[RequireLowerEnvironment]
public class JobsController(
    ReconcileAdditionalStorageJobHostedService jobsHostedService,
    ISchedulerFactory schedulerFactory) : Controller
{
    [HttpPost("run/{jobName}")]
    public async Task<IActionResult> RunJobAsync(string jobName)
    {
        if (jobName == nameof(ReconcileAdditionalStorageJob))
        {
            await ReconcileAdditionalStorageJob.RunJobNowAsync(schedulerFactory);
            return Ok(new { message = $"Job {jobName} scheduled successfully" });
        }

        return BadRequest(new { error = $"Unknown job name: {jobName}" });
    }

    [HttpPost("stop/{jobName}")]
    public async Task<IActionResult> StopJobAsync(string jobName)
    {
        if (jobName == nameof(ReconcileAdditionalStorageJob))
        {
            await jobsHostedService.InterruptJobsAndShutdownAsync();
            return Ok(new { message = $"Job {jobName} stopped successfully" });
        }

        return BadRequest(new { error = $"Unknown job name: {jobName}" });
    }
}
