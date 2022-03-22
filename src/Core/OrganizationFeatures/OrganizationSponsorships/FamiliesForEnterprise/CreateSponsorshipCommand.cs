using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public class CreateSponsorshipCommand : ICreateSponsorshipCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;

        public CreateSponsorshipCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
        }

        protected async Task<OrganizationSponsorship> CreateSponsorshipAsync(Organization sponsoringOrg, Guid? sponsoringOrgUserId,
            PlanSponsorshipType? sponsorshipType, string sponsoredEmail, string friendlyName)
        {
            var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(sponsorshipType.GetValueOrDefault())?.SponsoringProductType;
            if (requiredSponsoringProductType == null ||
                sponsoringOrg == null ||
                StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUserId.GetValueOrDefault());
            if (existingOrgSponsorship?.SponsoredOrganizationId != null)
            {
                throw new BadRequestException("Can only sponsor one organization per Organization User.");
            }

            var sponsorship = new OrganizationSponsorship
            {
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUserId,
                FriendlyName = friendlyName,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = sponsorshipType,
            };

            if (existingOrgSponsorship != null)
            {
                // Replace existing invalid offer with our new sponsorship offer
                sponsorship.Id = existingOrgSponsorship.Id;
            }

            try
            {
                await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
                return sponsorship;
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

        public async Task<OrganizationSponsorship> CreateSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType? sponsorshipType, string sponsoredEmail, string friendlyName)
        {
            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            return await CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser.Id, sponsorshipType, sponsoredEmail, friendlyName);
        }
    }
}
