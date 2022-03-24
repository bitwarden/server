using System;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class SelfHostedSponsorshipSyncJob : BaseJob
    {
        private GlobalSettings _globalSettings;

        public SelfHostedSponsorshipSyncJob(
            ILogger<SelfHostedSponsorshipSyncJob> logger,
            GlobalSettings globalSettings)
            : base(logger)
        {
            _globalSettings = globalSettings;
        }

        protected override Task ExecuteJobAsync(IJobExecutionContext context)
        {
            if (!_globalSettings.EnableCloudCommunication)
            {
                _logger.LogInformation($"Failed to sync instance with cloud - Cloud communication is disabled in global settings");
                return Task.CompletedTask;
            }

            // TODO: add job to sync sponsorships in self hosted organizations that support it

            return Task.CompletedTask;
        }
    }
}
