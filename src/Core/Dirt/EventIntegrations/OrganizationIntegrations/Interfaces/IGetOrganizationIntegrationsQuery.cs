using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;

/// <summary>
/// Query interface for retrieving organization integrations.
/// </summary>
public interface IGetOrganizationIntegrationsQuery
{
    /// <summary>
    /// Retrieves all organization integrations for a specific organization.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <returns>A list of organization integrations associated with the organization.</returns>
    Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId);
}
