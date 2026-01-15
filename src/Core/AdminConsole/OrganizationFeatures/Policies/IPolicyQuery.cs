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
    /// has been applied to an organization; the resultant <see cref="PolicyData"/> is not indicative of explicit
    /// policy configuration. 
    /// </remarks>
    Task<PolicyData> RunAsync(Guid organizationId, PolicyType policyType);
}
