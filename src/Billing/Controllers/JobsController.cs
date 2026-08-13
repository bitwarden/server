using Bit.Billing.Jobs;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Billing.Controllers;

/// <summary>
/// Lower-environment-only endpoint that lets QA fire scheduled billing jobs on demand instead of
/// waiting for their cron cadence. Returns 404 outside Development/QA via
/// <see cref="RequireLowerEnvironmentAttribute"/>.
/// </summary>
[Route("jobs")]
[SelfHosted(NotSelfHostedOnly = true)]
[RequireLowerEnvironment]
public class JobsController(
    JobsHostedService jobsHostedService) : Controller
{
    [HttpPost("run/{jobName}")]
    public async Task<IActionResult> RunJobAsync(string jobName)
    {
        if (jobName == nameof(SendInvoicePriceMigrationJob))
        {
            await jobsHostedService.TriggerJobNowAsync<SendInvoicePriceMigrationJob>();
            return Ok(new { message = $"Job {jobName} triggered successfully" });
        }

        return BadRequest(new { error = $"Unknown job name: {jobName}" });
    }
}
