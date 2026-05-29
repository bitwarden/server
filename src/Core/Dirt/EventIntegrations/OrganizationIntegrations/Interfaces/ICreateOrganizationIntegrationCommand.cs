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

    /// <summary>
    /// Checks if a new organization integration can be created based on existing integrations.
    /// Enforces a validation to ensure that only one integration of each type can exist per organization.
    /// </summary>
    /// <param name="integration"></param>
    /// <returns></returns>
    Task<bool> CanCreateAsync(OrganizationIntegration integration);
}
