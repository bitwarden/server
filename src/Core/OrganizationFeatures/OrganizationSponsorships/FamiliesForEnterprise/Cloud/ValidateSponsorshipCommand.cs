using System;
using System.Threading.Tasks;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class ValidateSponsorshipCommand : CloudCancelSponsorshipCommand, IValidateSponsorshipCommand
    {
        public ValidateSponsorshipCommand(
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService) : base(organizationSponsorshipRepository, organizationRepository, paymentService, mailService)
        {
        }

        public async Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId)
        {
            var sponsoredOrganization = await _organizationRepository.GetByIdAsync(sponsoredOrganizationId);
            if (sponsoredOrganization == null)
            {
                return false;
            }

            var existingSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrganizationId);

            if (existingSponsorship == null)
            {
                await CancelSponsorshipAsync(sponsoredOrganization, null);
                return false;
            }

            if (existingSponsorship.SponsoringOrganizationId == null || existingSponsorship.SponsoringOrganizationUserId == null || existingSponsorship.PlanSponsorshipType == null)
            {
                await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }
            var sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(existingSponsorship.PlanSponsorshipType.Value);

            var sponsoringOrganization = await _organizationRepository
                .GetByIdAsync(existingSponsorship.SponsoringOrganizationId);
            if (sponsoringOrganization == null)
            {
                await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            var sponsoringOrgPlan = Utilities.StaticStore.GetPlan(sponsoringOrganization.PlanType);
            if (!sponsoringOrganization.Enabled || sponsoredPlan.SponsoringProductType != sponsoringOrgPlan.Product)
            {
                await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            return true;
        }
    }
}
