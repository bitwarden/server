using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;

public class SelfHostedRevokeSponsorshipCommand : CancelSponsorshipCommand, IRevokeSponsorshipCommand
{
    public SelfHostedRevokeSponsorshipCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository) : base(organizationSponsorshipRepository, organizationRepository)
    {
    }

    public async Task RevokeSponsorshipAsync(OrganizationSponsorship sponsorship)
    {
        if (sponsorship == null)
        {
            throw new BadRequestException("You are not currently sponsoring an organization.");
        }

        if (sponsorship.LastSyncDate == null)
        {
            await base.DeleteSponsorshipAsync(sponsorship);
        }
        else
        {
            await MarkToDeleteSponsorshipAsync(sponsorship);
        }
    }
}
