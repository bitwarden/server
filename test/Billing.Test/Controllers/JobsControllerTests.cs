using Bit.Billing.Controllers;
using Bit.Billing.Jobs;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Xunit;

namespace Bit.Billing.Test.Controllers;

public class JobsControllerTests
{
    private readonly JobsHostedService _jobsHostedService = Substitute.For<JobsHostedService>(
        new GlobalSettings(),
        Substitute.For<IServiceProvider>(),
        Substitute.For<ILogger<JobsHostedService>>(),
        Substitute.For<ILogger<JobListener>>(),
        Substitute.For<ISchedulerFactory>());

    private JobsController CreateController() => new(_jobsHostedService);

    [Fact]
    public async Task RunJobAsync_SendInvoicePriceMigrationJob_TriggersJobAndReturnsOk()
    {
        var result = await CreateController().RunJobAsync(nameof(SendInvoicePriceMigrationJob));

        Assert.IsType<OkObjectResult>(result);
        await _jobsHostedService.Received(1).TriggerJobNowAsync<SendInvoicePriceMigrationJob>();
    }

    [Fact]
    public async Task RunJobAsync_UnknownJobName_ReturnsBadRequest()
    {
        var result = await CreateController().RunJobAsync("NotARealJob");

        Assert.IsType<BadRequestObjectResult>(result);
        await _jobsHostedService.DidNotReceiveWithAnyArgs().TriggerJobNowAsync<SendInvoicePriceMigrationJob>();
    }
}
