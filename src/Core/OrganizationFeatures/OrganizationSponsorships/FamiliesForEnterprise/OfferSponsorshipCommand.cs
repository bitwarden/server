using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public class OfferSponsorshipCommand : SendSponsorshipOfferCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;

        public OfferSponsorshipCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IUserRepository userRepository,
            IMailService mailService,
            IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> tokenFactory) : base(userRepository, mailService, tokenFactory)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
        }

        public async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName, string sponsoringUserEmail)
        {
            var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(sponsorshipType)?.SponsoringProductType;
            if (requiredSponsoringProductType == null ||
                sponsoringOrg == null ||
                StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship?.SponsoredOrganizationId != null)
            {
                throw new BadRequestException("Can only sponsor one organization per Organization User.");
            }

            var sponsorship = new OrganizationSponsorship
            {
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                FriendlyName = friendlyName,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = sponsorshipType,
                CloudSponsor = true,
            };

            if (existingOrgSponsorship != null)
            {
                // Replace existing invalid offer with our new sponsorship offer
                sponsorship.Id = existingOrgSponsorship.Id;
            }

            try
            {
                await _organizationSponsorshipRepository.UpsertAsync(sponsorship);

                await SendSponsorshipOfferAsync(sponsorship, sponsoringUserEmail);
            }
            catch
            {
                if (sponsorship.Id != default)
                {
                    await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
                }
                throw;
            }
        }
    }
}
