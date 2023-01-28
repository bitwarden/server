using Quartz;
using Quartz.Spi;

namespace Bit.Core.Jobs;

public class JobFactory : IJobFactory
{
    private readonly IServiceProvider _container;

    public JobFactory(IServiceProvider container)
    {
        _container = container;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return _container.GetService(bundle.JobDetail.JobType) as IJob;
    }

    public void ReturnJob(IJob job)
    {
        var disposable = job as IDisposable;
        disposable?.Dispose();
    }
}
