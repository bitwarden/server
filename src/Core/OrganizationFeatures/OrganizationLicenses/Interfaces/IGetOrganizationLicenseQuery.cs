using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface IGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}
