using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class RemoveSponsorshipCommand : CloudCancelSponsorshipCommand, IRemoveSponsorshipCommand
    {
        public RemoveSponsorshipCommand(
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService) : base(organizationSponsorshipRepository, organizationRepository, paymentService, mailService)
        {
        }

        public async Task RemoveSponsorshipAsync(Organization sponsoredOrg, OrganizationSponsorship sponsorship)
        {
            if (sponsorship == null || sponsorship.SponsoredOrganizationId == null)
            {
                throw new BadRequestException("The requested organization is not currently being sponsored.");
            }

            if (sponsoredOrg == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }

            await CancelSponsorshipAsync(sponsoredOrg, sponsorship);
        }
    }
}
