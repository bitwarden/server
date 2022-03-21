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

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class CloudSyncOrganizationSponsorshipsCommand : ICloudSyncOrganizationSponsorshipsCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICreateSponsorshipCommand _createSponsorshipCommand;
        private readonly ISendSponsorshipOfferCommand _sendSponsorshipOfferCommand;


        public CloudSyncOrganizationSponsorshipsCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICreateSponsorshipCommand createSponsorshipCommand,
        ISendSponsorshipOfferCommand sendSponsorshipOfferCommand)
        {
            _organizationUserRepository = organizationUserRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _createSponsorshipCommand = createSponsorshipCommand;
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
                    existingOrgSponsorship = await _createSponsorshipCommand.CreateSponsorshipAsync(
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

                syncResponseModel.SponsorshipsBatch.Append( 
                    new OrganizationSponsorshipModel {
                        SponsoringOrganizationUserId = selfHostedSponsorship.SponsoringOrganizationUserId,
                        FriendlyName = selfHostedSponsorship.FriendlyName,
                        OfferedToEmail = selfHostedSponsorship.OfferedToEmail,
                        PlanSponsorshipType = selfHostedSponsorship.PlanSponsorshipType,
                        // ValidUntil = selfHostedSponsorship.ValidUntil,
                        // ToDelete = selfHostedSponsorship.ToDelete
                    }
                );
            }

            return syncResponseModel;
        }

    }
}
