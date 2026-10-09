namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;

/// <summary>
/// Command interface for re-enabling the configurations of an organization integration that the circuit breaker
/// disabled.
/// </summary>
public interface IEnableOrganizationIntegrationCommand
{
    /// <summary>
    /// Re-enables every configuration under an integration that the circuit breaker disabled, without changing the
    /// integration itself. Clients must call this after fixing whatever caused the failures, because nothing
    /// re-enables a configuration on a timer.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="integrationId">The unique identifier of the integration.</param>
    /// <returns>The number of configurations re-enabled.</returns>
    /// <exception cref="Exceptions.BadRequestException">Thrown when the integration does not exist or does not
    /// belong to the specified organization.</exception>
    Task<int> EnableAsync(Guid organizationId, Guid integrationId);
}
