using Bit.Billing.Jobs;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Billing.Controllers;

[Route("jobs")]
[SelfHosted(NotSelfHostedOnly = true)]
[RequireLowerEnvironment]
public class JobsController(
    JobsHostedService jobsHostedService) : Controller
{
    [HttpPost("run/{jobName}")]
    public async Task<IActionResult> RunJobAsync(string jobName)
    {
        if (jobName == nameof(ReconcileAdditionalStorageJob))
        {
            await jobsHostedService.RunJobAdHocAsync<ReconcileAdditionalStorageJob>();
            return Ok(new { message = $"Job {jobName} scheduled successfully" });
        }

        return BadRequest(new { error = $"Unknown job name: {jobName}" });
    }

    [HttpPost("stop/{jobName}")]
    public async Task<IActionResult> StopJobAsync(string jobName)
    {
        if (jobName == nameof(ReconcileAdditionalStorageJob))
        {
            await jobsHostedService.InterruptAdHocJobAsync<ReconcileAdditionalStorageJob>();
            return Ok(new { message = $"Job {jobName} queued for cancellation" });
        }

        return BadRequest(new { error = $"Unknown job name: {jobName}" });
    }
}
