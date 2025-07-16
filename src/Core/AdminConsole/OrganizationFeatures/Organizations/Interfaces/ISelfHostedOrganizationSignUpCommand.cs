using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface ISelfHostedOrganizationSignUpCommand
{
    /// <summary>
    /// Create a new organization on a self-hosted instance
    /// </summary>
    Task<(Organization organization, OrganizationUser? organizationUser)> SignUpAsync(
        OrganizationLicense license, User owner, string ownerKey,
        string? collectionName, string publicKey, string privateKey);
}
