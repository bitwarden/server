using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class AliveJob : IJob
    {
        private readonly ILogger<AliveJob> _logger;

        public AliveJob(
            ILogger<AliveJob> logger)
        {
            _logger = logger;
        }

        public Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("It's alive!");
            return Task.FromResult(0);
        }
    }
}
