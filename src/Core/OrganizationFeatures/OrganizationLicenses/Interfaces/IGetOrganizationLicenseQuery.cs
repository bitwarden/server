using Bit.Core.Models.Business;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface IGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}
