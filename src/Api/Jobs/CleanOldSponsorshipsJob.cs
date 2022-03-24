using System;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class CleanOldSponsorshipsJob : BaseJob
    {
        private GlobalSettings _globalSettings;

        public CleanOldSponsorshipsJob(
            ILogger<CleanOldSponsorshipsJob> logger,
            GlobalSettings globalSettings)
            : base(logger)
        {
            _globalSettings = globalSettings;
        }

        protected override Task ExecuteJobAsync(IJobExecutionContext context)
        {
            // TODO: add job to clean old sponsorships

            return Task.CompletedTask;
        }
    }
}
