using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationLicenses;

public interface ICloudGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}
