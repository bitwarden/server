using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;
using Bit.Core.Models.Api.Response.OrganizationSponsorships;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class SelfHostedSyncSponsorshipsCommand : BaseIdentityClientService, ISelfHostedSyncSponsorshipsCommand
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
        private readonly ILicensingService _licensingService;

        public SelfHostedSyncSponsorshipsCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationConnectionRepository organizationConnectionRepository,
        ILicensingService licensingService,
        GlobalSettings globalSettings,
        ILogger<SelfHostedSyncSponsorshipsCommand> logger)
        : base(
            globalSettings.BaseServiceUri.InternalVault,
            globalSettings.BaseServiceUri.InternalIdentity,
            "api.installation",
            globalSettings.Installation.Id.ToString(),
            globalSettings.Installation.Key,
            logger)
        {
            _globalSettings = globalSettings;
            _organizationUserRepository = organizationUserRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationConnectionRepository = organizationConnectionRepository;
            _licensingService = licensingService;
        }

        public async Task SyncOrganization(Guid organizationId, OrganizationConnection billingSyncKey)
        {
            if (!_globalSettings.EnableCloudCommunication)
            {
                _logger.LogInformation("Failed to sync instance with cloud - Cloud communication is disabled in global settings");
                return;
            }
            if (!billingSyncKey.Enabled)
            {
                throw new BadRequestException($"Billing Sync Key disabled for organization {organizationId}");
            }
            if (string.IsNullOrWhiteSpace(billingSyncKey.Config))
            {
                throw new BadRequestException($"No Billing Sync Key known for organization {organizationId}");
            }

            var cloudOrganizationId = (await _licensingService.ReadOrganizationLicenseAsync(organizationId)).Id;
            var organizationSponsorships = await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(organizationId);
            var syncedSponsorships = new List<OrganizationSponsorshipData>();

            foreach (var orgSponsorshipsBatch in CoreHelpers.Batch(organizationSponsorships, 1000))
            {
                var response = await SendAsync<OrganizationSponsorshipSyncRequestModel, OrganizationSponsorshipSyncResponseModel>(HttpMethod.Post, "organizationSponsorships/sync", new OrganizationSponsorshipSyncRequestModel
                {
                    BillingSyncKey = billingSyncKey.Config,
                    SponsoringOrganizationCloudId = cloudOrganizationId,
                    SponsorshipsBatch = orgSponsorshipsBatch.Select(s => new OrganizationSponsorshipRequestModel
                    {
                        SponsoringOrganizationUserId = s.SponsoringOrganizationUserId,
                        FriendlyName = s.FriendlyName,
                        OfferedToEmail = s.OfferedToEmail,
                        PlanSponsorshipType = s.PlanSponsorshipType.GetValueOrDefault(),
                        ValidUntil = s.ValidUntil,
                        ToDelete = s.ToDelete
                    })
                });

                if (response == null)
                {
                    throw new BadRequestException("Organization sync failed");
                }

                syncedSponsorships.AddRange(response.ToOrganizationSponsorshipSync().SponsorshipsBatch);
            }

            var organizationSponsorshipsDict = organizationSponsorships.ToDictionary(i => i.SponsoringOrganizationUserId);
            var sponsorshipsToDelete = syncedSponsorships.Where(s => s.CloudSponsorshipRemoved).Select(i => organizationSponsorshipsDict[i.SponsoringOrganizationUserId].Id);
            var sponsorshipsToUpsert = syncedSponsorships.Where(s => !s.CloudSponsorshipRemoved).Select(i =>
            {
                var existingSponsorship = organizationSponsorshipsDict[i.SponsoringOrganizationUserId];
                if (existingSponsorship != null)
                {
                    existingSponsorship.LastSyncDate = i.LastSyncDate;
                    existingSponsorship.ValidUntil = i.ValidUntil;
                    existingSponsorship.ToDelete = i.ToDelete;

                }
                else
                {
                    existingSponsorship = new OrganizationSponsorship
                    {
                        SponsoringOrganizationId = organizationId,
                        SponsoringOrganizationUserId = i.SponsoringOrganizationUserId,
                        FriendlyName = i.FriendlyName,
                        OfferedToEmail = i.OfferedToEmail,
                        PlanSponsorshipType = i.PlanSponsorshipType,
                        LastSyncDate = i.LastSyncDate,
                        ValidUntil = i.ValidUntil,
                        ToDelete = i.ToDelete
                    };
                }
                return existingSponsorship;
            });

            await _organizationSponsorshipRepository.DeleteManyAsync(sponsorshipsToDelete);
            await _organizationSponsorshipRepository.UpsertManyAsync(sponsorshipsToUpsert);
        }

    }
}
