using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class OrganizationSponsorshipRenewCommand : IOrganizationSponsorshipRenewCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;

    public OrganizationSponsorshipRenewCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
    }

    public async Task UpdateExpirationDateAsync(Guid organizationId, DateTime expireDate)
    {
        var sponsorship = await _organizationSponsorshipRepository.GetBySponsoredOrganizationIdAsync(organizationId);

        if (sponsorship == null)
        {
            return;
        }

        sponsorship.ValidUntil = expireDate;
        await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
    }
}
