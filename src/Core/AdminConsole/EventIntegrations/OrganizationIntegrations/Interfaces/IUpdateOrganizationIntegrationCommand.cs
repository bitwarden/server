using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;

/// <summary>
/// Command interface for updating organization integrations.
/// </summary>
public interface IUpdateOrganizationIntegrationCommand
{
    /// <summary>
    /// Updates an existing organization integration.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="integrationId">The unique identifier of the integration to update.</param>
    /// <param name="updatedIntegration">The updated organization integration data.</param>
    /// <returns>The updated organization integration.</returns>
    /// <exception cref="Exceptions.NotFoundException">Thrown when the integration does not exist,
    /// does not belong to the specified organization, or the integration type does not match.</exception>
    Task<OrganizationIntegration> UpdateAsync(Guid organizationId, Guid integrationId, OrganizationIntegration updatedIntegration);
}
