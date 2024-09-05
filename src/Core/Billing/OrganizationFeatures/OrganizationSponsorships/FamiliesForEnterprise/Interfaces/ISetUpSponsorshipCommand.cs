using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface ISetUpSponsorshipCommand
{
    Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
        Organization sponsoredOrganization);
}
