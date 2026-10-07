using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyQuery
{
    /// <summary>
    /// Retrieves a summary view of an organization's usage of a policy specified by the <paramref name="policyType"/>.
    /// </summary>
    /// <remarks>
    /// This query is the entrypoint for consumers interested in understanding how a particular <see cref="PolicyType"/>
    /// has been applied to an organization; the resultant <see cref="PolicyStatus"/> is not indicative of explicit
    /// policy configuration.
    /// </remarks>
    Task<PolicyStatus> RunAsync(Guid organizationId, PolicyType policyType);

    /// <summary>
    /// Retrieves all policies for an organization.
    /// </summary>
    /// <remarks>
    /// Introduced to isolate the aggregation logic used to construct SendControls policy status
    /// from legacy Send policy statuss. May be removed if and when a SendControls migration runs.
    /// </remarks>
    Task<IEnumerable<PolicyStatus>> GetAllAsync(Guid organizationId);
}
