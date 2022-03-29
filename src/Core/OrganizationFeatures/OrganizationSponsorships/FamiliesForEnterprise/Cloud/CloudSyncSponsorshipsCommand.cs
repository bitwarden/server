using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class CloudSyncSponsorshipsCommand : CreateSponsorshipCommand, ICloudSyncSponsorshipsCommand
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ISendSponsorshipOfferCommand _sendSponsorshipOfferCommand;


        public CloudSyncSponsorshipsCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        ISendSponsorshipOfferCommand sendSponsorshipOfferCommand) : base(organizationSponsorshipRepository)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _sendSponsorshipOfferCommand = sendSponsorshipOfferCommand;
        }

        public async Task<OrganizationSponsorshipSyncData> SyncOrganization(OrganizationSponsorshipSyncData syncData)
        {

            var sponsoringOrg = await _organizationRepository.GetByIdAsync(syncData.SponsoringOrganizationCloudId);
            if (sponsoringOrg == null)
            {
                throw new BadRequestException("Failed to sync sponsorship - missing organization");
            }

            var existingSponsorships = await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(sponsoringOrg.Id);
            var existingSponsorshipsDict = existingSponsorships.ToDictionary(i => i.SponsoringOrganizationUserId);

            var sponsorshipsToUpsert = new List<OrganizationSponsorship>();
            var sponsorshipIdsToDelete = new List<Guid>();

            foreach (var selfHostedSponsorship in syncData.SponsorshipsBatch)
            {
                var cloudSponsorship = existingSponsorshipsDict[selfHostedSponsorship.SponsoringOrganizationUserId];
                if (cloudSponsorship == null)
                {
                    if (selfHostedSponsorship.ToDelete && selfHostedSponsorship.LastSyncDate == null)
                    {
                        continue; // prevent invalid sponsorships in cloud. These should have been deleted by self hosted
                    }
                    cloudSponsorship = new OrganizationSponsorship
                    {
                        SponsoringOrganizationId = sponsoringOrg.Id,
                        SponsoringOrganizationUserId = selfHostedSponsorship.SponsoringOrganizationUserId,
                        SponsoredOrganizationId = selfHostedSponsorship.SponsoredOrganizationId,
                        FriendlyName = selfHostedSponsorship.FriendlyName,
                        OfferedToEmail = selfHostedSponsorship.OfferedToEmail,
                        PlanSponsorshipType = selfHostedSponsorship.PlanSponsorshipType,
                        LastSyncDate = DateTime.UtcNow,
                        ValidUntil = selfHostedSponsorship.ValidUntil,
                        ToDelete = selfHostedSponsorship.ToDelete
                    };
                    sponsorshipsToUpsert.Add(cloudSponsorship);
                }
                else
                {
                    cloudSponsorship.LastSyncDate = DateTime.UtcNow;
                    sponsorshipsToUpsert.Add(cloudSponsorship);
                }

                if (selfHostedSponsorship.ToDelete)
                {
                    if (cloudSponsorship.SponsoredOrganizationId == null)
                    {
                        sponsorshipIdsToDelete.Add(cloudSponsorship.Id);
                        selfHostedSponsorship.CloudSponsorshipRemoved = true;
                    }
                    else
                    {
                        cloudSponsorship.ToDelete = true;
                    }
                }

                if (selfHostedSponsorship.SponsoredOrganizationId == null)
                {
                    if (cloudSponsorship.SponsoredOrganizationId != null)
                    {
                        selfHostedSponsorship.SponsoredOrganizationId = cloudSponsorship.SponsoredOrganizationId.GetValueOrDefault();
                        selfHostedSponsorship.ValidUntil = cloudSponsorship.ValidUntil;
                    }
                }

                selfHostedSponsorship.LastSyncDate = DateTime.UtcNow;
            }

            await _organizationSponsorshipRepository.UpsertManyAsync(sponsorshipsToUpsert);
            await _sendSponsorshipOfferCommand.BulkSendSponsorshipOfferAsync(sponsoringOrg.Name, sponsorshipsToUpsert.Where(s => s.Id == null));
            await _organizationSponsorshipRepository.DeleteManyAsync(sponsorshipIdsToDelete);


            return syncData;
        }

    }
}
