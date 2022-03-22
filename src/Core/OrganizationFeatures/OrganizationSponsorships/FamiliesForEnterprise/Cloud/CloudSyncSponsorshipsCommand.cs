using System.Linq;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using System;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class CloudSyncOrganizationSponsorshipsCommand : CreateSponsorshipCommand, ICloudSyncOrganizationSponsorshipsCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ISendSponsorshipOfferCommand _sendSponsorshipOfferCommand;


        public CloudSyncOrganizationSponsorshipsCommand (
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        ISendSponsorshipOfferCommand sendSponsorshipOfferCommand) : base(organizationSponsorshipRepository)
        {
            _organizationUserRepository = organizationUserRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _sendSponsorshipOfferCommand = sendSponsorshipOfferCommand;
        }

        public async Task<OrganizationSponsorshipSyncModel> SyncOrganization(Organization sponsoringOrg, IEnumerable<OrganizationSponsorshipModel> sponsorshipsBatch)
        {

            var syncResponseModel = new OrganizationSponsorshipSyncModel
            {
                SponsoringOrganizationCloudId = sponsoringOrg.Id,
            };


            foreach (var selfHostedSponsorship in sponsorshipsBatch)
            {
                if (selfHostedSponsorship.SponsoringOrganizationUserId == null)
                {
                    throw new BadRequestException("Cannot sync sponsorship - missing user");
                }
                
                var existingOrgSponsorship = await _organizationSponsorshipRepository
                    .GetBySponsoringOrganizationUserIdAsync(selfHostedSponsorship.SponsoringOrganizationUserId.GetValueOrDefault());
                
                if (existingOrgSponsorship == null)
                {
                    existingOrgSponsorship = await CreateSponsorshipAsync(
                        sponsoringOrg, 
                        selfHostedSponsorship.SponsoringOrganizationUserId, 
                        selfHostedSponsorship.PlanSponsorshipType, 
                        selfHostedSponsorship.OfferedToEmail, selfHostedSponsorship.FriendlyName);

                    await _sendSponsorshipOfferCommand.SendSponsorshipOfferAsync(existingOrgSponsorship, sponsoringOrg.Name);
                }

                if (selfHostedSponsorship.ToDelete)
                {
                    if (existingOrgSponsorship.SponsoredOrganizationId == null)
                    {
                        await _organizationSponsorshipRepository.DeleteAsync(existingOrgSponsorship);

                        selfHostedSponsorship.CloudSponsorshipRemoved = true;
                        syncResponseModel.SponsorshipsBatch.Append(selfHostedSponsorship);
                        continue;
                    }
                    else 
                    {
                        // existingOrgSponsorship.ToDelete = selfHostedSponsorship.ToDelete;
                    }

                }

                if (selfHostedSponsorship.SponsoredOrganizationId == null)
                {
                    if (existingOrgSponsorship.SponsoredOrganizationId != null)
                    {
                        selfHostedSponsorship.SponsoredOrganizationId = existingOrgSponsorship.SponsoredOrganizationId;
                        // selfHostedSponsorship.ValidUntil = existingOrgSponsorship.ValidUntil;
                    }
                }

                selfHostedSponsorship.LastSyncDate = DateTime.UtcNow;

                syncResponseModel.SponsorshipsBatch.Append(selfHostedSponsorship);
            }

            return syncResponseModel;
        }

    }
}
