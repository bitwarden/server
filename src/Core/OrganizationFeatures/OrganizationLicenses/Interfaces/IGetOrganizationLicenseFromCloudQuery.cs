using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface IGetOrganizationLicenseFromCloudQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Guid organizationId, OrganizationConnection billingSyncConnection);
}
