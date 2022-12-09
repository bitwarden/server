using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface ICloudGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}

public interface ISelfHostedGetOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, OrganizationConnection billingSyncConnection);
}
