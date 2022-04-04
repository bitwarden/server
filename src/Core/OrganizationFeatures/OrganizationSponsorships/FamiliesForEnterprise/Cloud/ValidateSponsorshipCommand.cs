using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class ValidateSponsorshipCommand : CancelSponsorshipCommand, IValidateSponsorshipCommand
    {
        private readonly IPaymentService _paymentService;
        private readonly IMailService _mailService;
        private readonly ILogger<ValidateSponsorshipCommand> _logger;

        public ValidateSponsorshipCommand(
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService,
            ILogger<ValidateSponsorshipCommand> logger) : base(organizationSponsorshipRepository, organizationRepository)
        {
            _paymentService = paymentService;
            _mailService = mailService;
            _logger = logger;
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

            if (existingSponsorship.SponsoringOrganizationId == default || existingSponsorship.SponsoringOrganizationUserId == default || existingSponsorship.PlanSponsorshipType == null)
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
            if (!sponsoringOrganization.Enabled ||
                sponsoredPlan.SponsoringProductType != sponsoringOrgPlan.Product ||
                existingSponsorship.ToDelete)
            {
                await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            return true;
        }

        protected async Task CancelSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship = null)
        {
            if (sponsoredOrganization != null)
            {
                await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization, sponsorship);
                await _organizationRepository.UpsertAsync(sponsoredOrganization);

                try
                {
                    await _mailService.SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(
                        sponsoredOrganization.BillingEmailAddress(),
                        sponsoredOrganization.Name);
                }
                catch (Exception e)
                {
                    _logger.LogError("Error sending Family sponsorship removed email.", e);
                }
            }
            await base.DeleteSponsorshipAsync(sponsorship);
        }
    }
}
