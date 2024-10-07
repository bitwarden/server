using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Billing.SelfHostLicenses.OrganizationLicenses;

public interface ICloudGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}
