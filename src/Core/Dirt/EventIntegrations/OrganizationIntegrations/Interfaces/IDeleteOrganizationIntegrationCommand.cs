namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;

/// <summary>
/// Command interface for deleting organization integrations.
/// </summary>
public interface IDeleteOrganizationIntegrationCommand
{
    /// <summary>
    /// Deletes an organization integration.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="integrationId">The unique identifier of the integration to delete.</param>
    /// <exception cref="Exceptions.NotFoundException">Thrown when the integration does not exist
    /// or does not belong to the specified organization.</exception>
    Task DeleteAsync(Guid organizationId, Guid integrationId);
}
