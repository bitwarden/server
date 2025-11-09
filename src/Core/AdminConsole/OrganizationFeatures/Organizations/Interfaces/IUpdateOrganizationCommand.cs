using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IUpdateOrganizationCommand
{
    /// <summary>
    /// Updates an organization's information in the Bitwarden database and Stripe (if required).
    /// Also optionally updates an organization's public/private keypair if it was not created with one.
    /// </summary>
    /// <param name="request">The update request containing the details to be updated.</param>
    Task<Organization> UpdateAsync(UpdateOrganizationRequest request);
}
