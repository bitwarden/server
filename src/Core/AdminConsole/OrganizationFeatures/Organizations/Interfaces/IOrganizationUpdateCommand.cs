using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Update;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationUpdateCommand
{
    /// <summary>
    /// Updates an organization's information in the Bitwarden database and Stripe (if required).
    /// Also optionally updates an organization's public-private keypair if it was not created with one.
    /// On self-host, only the public-private keys will be updated because all other properties are fixed by the license file.
    /// </summary>
    /// <param name="request">The update request containing the details to be updated.</param>
    Task<Organization> UpdateAsync(OrganizationUpdateRequest request);
}
