using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface ICreateSponsorshipCommand
{
    Task<OrganizationSponsorship> CreateSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName);
}
