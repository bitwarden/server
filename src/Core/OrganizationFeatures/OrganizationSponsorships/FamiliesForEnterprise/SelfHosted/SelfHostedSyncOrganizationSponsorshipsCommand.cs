using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class SelfHostedSyncOrganizationSponsorshipsCommand : BaseIdentityClientService, ISelfHostedSyncOrganizationSponsorshipsCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;

        private readonly ILicensingService _licensingService;

        public SelfHostedSyncOrganizationSponsorshipsCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        ILicensingService licensingService,
        IGlobalSettings globalSettings,
        ILogger<SelfHostedSyncOrganizationSponsorshipsCommand> logger) : base("vault.bitwarden.com", "identity.bitwarden.com", "api.installation", globalSettings.Installation.Id.ToString(), globalSettings.Installation.Key, logger)
        {
            _organizationUserRepository = organizationUserRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _licensingService = licensingService;
        }

        public async Task SyncOrganization(Guid organizationId)
        {
            var billingSyncKey = await GetBillingSyncKey(organizationId);

            if (string.IsNullOrWhiteSpace(billingSyncKey))
            {
                throw new BadRequestException($"No Billing Sync Key known for organization {organizationId}");
            }

            var cloudOrganizationId = (await _licensingService.ReadOrganizationLicenseAsync(organizationId)).Id;
            var orgUsers = await _organizationUserRepository.GetManyByOrganizationAsync(organizationId, null);
            var organizationSponsorships = await _organizationSponsorshipRepository.GetBySponsoringOrganizationAsync(organizationId);

            foreach (var orgSponsorshipsBatch in CoreHelpers.Batch(organizationSponsorships, 1000))
            {
                var response = await SendAsync<OrganizationSponsorshipSyncModel, OrganizationSponsorshipSyncModel>(HttpMethod.Post, "organizationSponsorships/sync", new OrganizationSponsorshipSyncModel
                {
                    SponsoringOrganizationCloudId = cloudOrganizationId,
                    SponsorshipsBatch = orgSponsorshipsBatch.Select(s => new OrganizationSponsorshipModel
                    {
                        SponsoringOrganizationUserId = s.SponsoringOrganizationUserId,
                        FriendlyName = s.FriendlyName,
                        OfferedToEmail = s.OfferedToEmail,
                        PlanSponsorshipType = s.PlanSponsorshipType,
                        // ValidUntil = s.ValidUntil,
                        // ToDelete = s.ToDelete
                    })
                });

                foreach (var sponsorshipModel in response.SponsorshipsBatch)
                {
                    var existingOrgSponsorship = await _organizationSponsorshipRepository
                        .GetBySponsoringOrganizationUserIdAsync(sponsorshipModel.SponsoringOrganizationUserId.GetValueOrDefault());
                    if (existingOrgSponsorship == null)
                    {
                        break;
                    }

                    if (sponsorshipModel.CloudSponsorshipRemoved)
                    {
                        await _organizationSponsorshipRepository.DeleteAsync(existingOrgSponsorship);
                    }

                    if (sponsorshipModel.LastSyncDate != null)
                    {
                        existingOrgSponsorship.LastSyncDate = sponsorshipModel.LastSyncDate;
                    }
                    
                    if (sponsorshipModel.ToDelete)
                    {
                        // existingOrgSponsorship.ToDelete = sponsorshipModel.ToDelete;
                    }

                    if (existingOrgSponsorship.SponsoredOrganizationId == null)
                    {
                        if (sponsorshipModel.SponsoredOrganizationId != null)
                        {
                            existingOrgSponsorship.SponsoredOrganizationId  = sponsorshipModel.SponsoredOrganizationId;
                            // existingOrgSponsorship.ValidUntil = sponsorshipModel.ValidUntil;
                        }
                    }
                }
            }
        }

        private Task<string> GetBillingSyncKey(Guid organizationId) => throw new NotImplementedException();
    }
}
