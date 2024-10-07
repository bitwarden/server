using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Billing.Licenses.OrganizationLicenses;

public interface ICloudGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}
