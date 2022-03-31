using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Jobs;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class SelfHostedSponsorshipSyncJob : BaseJob
    {
        private IOrganizationRepository _organizationRepository;
        private IOrganizationConnectionRepository _organizationConnectionRepository;
        private ISelfHostedSyncSponsorshipsCommand _syncSponsorshipsCommand;
        private GlobalSettings _globalSettings;

        public SelfHostedSponsorshipSyncJob(
            IOrganizationRepository organizationRepository,
            IOrganizationConnectionRepository organizationConnectionRepository,
            ISelfHostedSyncSponsorshipsCommand syncSponsorshipsCommand,
            ILogger<SelfHostedSponsorshipSyncJob> logger,
            GlobalSettings globalSettings)
            : base(logger)
        {
            _organizationRepository = organizationRepository;
            _organizationConnectionRepository = organizationConnectionRepository;
            _syncSponsorshipsCommand = syncSponsorshipsCommand;
            _globalSettings = globalSettings;
        }

        protected override async Task ExecuteJobAsync(IJobExecutionContext context)
        {
            if (!_globalSettings.EnableCloudCommunication)
            {
                _logger.LogInformation($"Failed to sync instance with cloud - Cloud communication is disabled in global settings");
                return;
            }

            var organizations = await _organizationRepository.GetManyByEnabledAsync();

            foreach (var org in organizations)
            {
                var connection = (await _organizationConnectionRepository.GetEnabledByOrganizationIdTypeAsync(org.Id, OrganizationConnectionType.CloudBillingSync)).FirstOrDefault();
                if (connection != null)
                {
                    try
                    {
                        await _syncSponsorshipsCommand.SyncOrganization(org.Id, connection);
                    }
                    catch
                    {
                        _logger.LogInformation($"Failed to sync {0} sponsorships with cloud", org.Name);
                    }
                }
            }

        }
    }
}
