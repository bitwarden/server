using System.Threading.Tasks;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class EmergencyAccessTimeoutJob : BaseJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public EmergencyAccessTimeoutJob(IServiceScopeFactory serviceScopeFactory, ILogger<EmergencyAccessNotificationJob> logger)
            : base(logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteJobAsync(IJobExecutionContext context)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var emergencyAccessService = scope.ServiceProvider.GetService(typeof(IEmergencyAccessService)) as IEmergencyAccessService;
            await emergencyAccessService.HandleTimedOutRequestsAsync();
        }
    }
}
