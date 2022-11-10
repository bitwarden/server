using Bit.Core.Models.Business;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface IUpdateOrganizationLicenseCommand
{
    Task UpdateLicenseAsync(Organization organization, OrganizationLicense license);
}
