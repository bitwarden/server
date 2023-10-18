#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Organizations;
using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;

public interface IUpdateOrganizationLicenseCommand
{
    Task UpdateLicenseAsync(SelfHostedOrganizationDetails selfHostedOrganization,
        OrganizationLicense license, Organization? currentOrganizationUsingLicenseKey);
}
