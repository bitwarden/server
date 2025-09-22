using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Queries;

public interface IGetTaskMetricsForOrganizationQuery
{
    /// <summary>
    /// Retrieves security task metrics for an organization.
    /// </summary>
    /// <param name="organizationId">The Id of the organization</param>
    /// <returns>Metrics for all security tasks within an organization.</returns>
    Task<SecurityTaskMetrics> GetTaskMetrics(Guid organizationId);
}
