using Bit.Core.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface IUpdateOrganizationLicenseCommand
{
    Task UpdateLicenseAsync(Organization organization, OrganizationLicense license, Organization? existingOrganization, 
        OrganizationPlanUsage planUsage);
}
