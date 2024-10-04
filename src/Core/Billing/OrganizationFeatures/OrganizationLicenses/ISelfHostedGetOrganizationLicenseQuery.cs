using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationLicenses;

public interface ISelfHostedGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, OrganizationConnection billingSyncConnection);
}
