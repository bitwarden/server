using Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class RemoveSponsorshipCommand : CancelSponsorshipCommand, IRemoveSponsorshipCommand
{
    public RemoveSponsorshipCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository) : base(organizationSponsorshipRepository, organizationRepository)
    {
    }

    public async Task RemoveSponsorshipAsync(OrganizationSponsorship sponsorship)
    {
        if (sponsorship == null || sponsorship.SponsoredOrganizationId == null)
        {
            throw new BadRequestException("The requested organization is not currently being sponsored.");
        }

        await MarkToDeleteSponsorshipAsync(sponsorship);
    }
}
