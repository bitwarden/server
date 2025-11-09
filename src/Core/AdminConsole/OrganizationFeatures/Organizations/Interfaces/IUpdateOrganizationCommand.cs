using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IUpdateOrganizationCommand
{
    /// <summary>
    /// Updates an organization's information and optionally updates billing details in Stripe.
    /// </summary>
    /// <param name="request">The update request containing the organization and billing update flag.</param>
    /// <exception cref="System.ApplicationException">Thrown when attempting to create an organization using this method.</exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">Thrown when the identifier is already in use by another organization.</exception>
    Task<Organization> UpdateAsync(UpdateOrganizationRequest request);
}
