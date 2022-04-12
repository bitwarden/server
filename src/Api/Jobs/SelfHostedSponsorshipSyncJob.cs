using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Jobs;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
        private readonly ILicensingService _licensingService;
        private ISelfHostedSyncSponsorshipsCommand _syncSponsorshipsCommand;
        private GlobalSettings _globalSettings;

        public SelfHostedSponsorshipSyncJob(
            IOrganizationRepository organizationRepository,
            IOrganizationConnectionRepository organizationConnectionRepository,
            ILicensingService licensingService,
            ISelfHostedSyncSponsorshipsCommand syncSponsorshipsCommand,
            ILogger<SelfHostedSponsorshipSyncJob> logger,
            GlobalSettings globalSettings)
            : base(logger)
        {
            _organizationRepository = organizationRepository;
            _organizationConnectionRepository = organizationConnectionRepository;
            _licensingService = licensingService;
            _syncSponsorshipsCommand = syncSponsorshipsCommand;
            _globalSettings = globalSettings;
        }

        protected override async Task ExecuteJobAsync(IJobExecutionContext context)
        {
            if (!_globalSettings.EnableCloudCommunication)
            {
                _logger.LogInformation("Skipping Organization sync with cloud - Cloud communication is disabled in global settings");
                return;
            }

            var organizations = await _organizationRepository.GetManyByEnabledAsync();

            foreach (var org in organizations)
            {
                var connection = (await _organizationConnectionRepository.GetEnabledByOrganizationIdTypeAsync(org.Id, OrganizationConnectionType.CloudBillingSync)).FirstOrDefault();
                if (connection != null)
                {
                    Guid cloudOrganizationId = new Guid();
                    try
                    {
                        cloudOrganizationId = (await _licensingService.ReadOrganizationLicenseAsync(org.Id)).Id;
                        if (cloudOrganizationId == default)
                        {
                            throw new Exception();
                        }
                    }
                    catch
                    {
                        _logger.LogInformation($"Skipping {org.Name} sponsorships sync with cloud - Billing Sync is not set up for the organization.");
                    }
                    try
                    {
                        await _syncSponsorshipsCommand.SyncOrganization(org.Id, cloudOrganizationId, connection);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Sponsorship sync for organization {org.Name} Failed");
                    }
                }
            }
        }
    }
}
