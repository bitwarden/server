using Bit.Core.Models.Business;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface IUpdateLicenseCommand
{
    Task UpdateLicenseAsync(Organization organization, OrganizationLicense license);
}
