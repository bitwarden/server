using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;

/// <summary>
/// Command interface for creating an OrganizationIntegration.
/// </summary>
public interface ICreateOrganizationIntegrationCommand
{
    /// <summary>
    /// Creates a new organization integration.
    /// </summary>
    /// <param name="integration">The OrganizationIntegration to create.</param>
    /// <returns>The created OrganizationIntegration.</returns>
    /// <exception cref="Exceptions.BadRequestException">Thrown when an integration
    /// of the same type already exists for the organization.</exception>
    Task<OrganizationIntegration> CreateAsync(OrganizationIntegration integration);
}
