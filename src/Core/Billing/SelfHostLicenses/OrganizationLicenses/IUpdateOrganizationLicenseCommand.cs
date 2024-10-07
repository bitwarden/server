#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Billing.SelfHostLicenses.OrganizationLicenses;

public interface IUpdateOrganizationLicenseCommand
{
    Task UpdateLicenseAsync(SelfHostedOrganizationDetails selfHostedOrganization,
        OrganizationLicense license, Organization? currentOrganizationUsingLicenseKey);
}
