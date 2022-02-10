using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class CloudSetUpSponsorshipCommand : ISetUpSponsorshipCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IPaymentService _paymentService;

        public CloudSetUpSponsorshipCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository, IOrganizationRepository organizationRepository, IPaymentService paymentService)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _paymentService = paymentService;
        }

        public async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
            Organization sponsoredOrganization)
        {
            if (sponsorship == null)
            {
                throw new BadRequestException("No unredeemed sponsorship offer exists for you.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrganization.Id);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.");
            }

            if (sponsorship.PlanSponsorshipType == null)
            {
                throw new BadRequestException("Cannot set up sponsorship without a known sponsorship type.");
            }

            // Check org to sponsor's product type
            var requiredSponsoredProductType = StaticStore.GetSponsoredPlan(sponsorship.PlanSponsorshipType.Value)?.SponsoredProductType;
            if (requiredSponsoredProductType == null ||
                sponsoredOrganization == null ||
                StaticStore.GetPlan(sponsoredOrganization.PlanType).Product != requiredSponsoredProductType.Value)
            {
                throw new BadRequestException("Can only redeem sponsorship offer on families organizations.");
            }

            await _paymentService.SponsorOrganizationAsync(sponsoredOrganization, sponsorship);
            await _organizationRepository.UpsertAsync(sponsoredOrganization);

            sponsorship.SponsoredOrganizationId = sponsoredOrganization.Id;
            sponsorship.OfferedToEmail = null;
            await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
        }
    }
}
