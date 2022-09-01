using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;

public abstract class CancelSponsorshipCommand
{
    protected readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    protected readonly IOrganizationRepository _organizationRepository;

    public CancelSponsorshipCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _organizationRepository = organizationRepository;
    }

    protected virtual async Task DeleteSponsorshipAsync(OrganizationSponsorship sponsorship = null)
    {
        if (sponsorship == null)
        {
            return;
        }

        await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
    }

    protected async Task MarkToDeleteSponsorshipAsync(OrganizationSponsorship sponsorship)
    {
        if (sponsorship == null)
        {
            throw new BadRequestException("The sponsorship you are trying to cancel does not exist");
        }

        sponsorship.ToDelete = true;
        await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
    }
}
