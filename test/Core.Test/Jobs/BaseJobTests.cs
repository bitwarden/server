using System.Diagnostics;
using Bit.Core.Jobs;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Xunit;

namespace Bit.Core.Test.Jobs;

public class BaseJobTests
{
    [Fact]
    public async Task Execute_RunsInsideAnActivityThatRecordsFailure()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Bitwarden.Jobs",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var job = new ThrowingJob();

        await job.Execute(Substitute.For<IJobExecutionContext>());

        var activity = Assert.Single(stopped, a => a == job.Current);
        Assert.Equal(nameof(ThrowingJob), activity.OperationName);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    private class ThrowingJob() : BaseJob(Substitute.For<ILogger>())
    {
        public Activity? Current { get; private set; }

        protected override Task ExecuteJobAsync(IJobExecutionContext context)
        {
            Current = Activity.Current;
            throw new InvalidOperationException();
        }
    }
}
