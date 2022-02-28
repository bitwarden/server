using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public abstract class CloudCancelSponsorshipCommand : CancelSponsorshipCommand
    {
        private readonly IPaymentService _paymentService;
        private readonly IMailService _mailService;

        public CloudCancelSponsorshipCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService) : base(organizationSponsorshipRepository, organizationRepository)
        {
            _paymentService = paymentService;
            _mailService = mailService;
        }


        protected async Task CancelSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship = null)
        {
            if (sponsoredOrganization != null)
            {
                await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization, sponsorship);
                await _organizationRepository.UpsertAsync(sponsoredOrganization);

                await _mailService.SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(
                    sponsoredOrganization.BillingEmailAddress(),
                    sponsoredOrganization.Name);
            }
            await base.CancelSponsorshipAsync(sponsorship);
        }
    }
}
