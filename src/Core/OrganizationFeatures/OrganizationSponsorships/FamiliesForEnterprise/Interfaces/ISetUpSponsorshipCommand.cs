using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface ISetUpSponsorshipCommand
{
    Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
        Organization sponsoredOrganization);
}
