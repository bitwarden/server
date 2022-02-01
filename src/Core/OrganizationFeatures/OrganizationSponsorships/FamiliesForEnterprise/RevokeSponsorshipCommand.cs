using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public class RevokeSponsorshipCommand : CancelSponsorshipCommand, IRevokeSponsorshipCommand
    {
        public RevokeSponsorshipCommand(
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService) : base(organizationSponsorshipRepository, organizationRepository, paymentService, mailService)
        {
        }

        public async Task RevokeSponsorshipAsync(Organization sponsoredOrg, OrganizationSponsorship sponsorship)
        {
            if (sponsorship == null)
            {
                throw new BadRequestException("You are not currently sponsoring an organization.");
            }

            if (sponsorship.SponsoredOrganizationId == null)
            {
                await CancelSponsorshipAsync(null, sponsorship);
                return;
            }

            if (sponsoredOrg == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }

            await CancelSponsorshipAsync(sponsoredOrg, sponsorship);
        }
    }
}
