using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class SelfHostedRevokeSponsorshipCommand : CancelSponsorshipCommand, ISelfHostedRevokeSponsorshipCommand
    {
        public SelfHostedRevokeSponsorshipCommand(
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository) : base(organizationSponsorshipRepository, organizationRepository)
        {
        }

        public async Task RevokeSponsorshipAsync(Organization sponsoredOrg, OrganizationSponsorship sponsorship)
        {
            if (sponsorship == null)
            {
                throw new BadRequestException("You are not currently sponsoring an organization.");
            }

            await CancelSponsorshipAsync(sponsorship);
        }
    }
}
