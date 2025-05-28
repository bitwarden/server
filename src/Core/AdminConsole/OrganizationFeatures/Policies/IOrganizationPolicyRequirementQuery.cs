#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// Query interface for retrieving policy requirements based on organization ID
/// </summary>
public interface IOrganizationPolicyRequirementQuery
{
    /// <summary>
    /// Gets a policy requirement of type T for an organization
    /// </summary>
    /// <typeparam name="T">The type of policy requirement to retrieve</typeparam>
    /// <param name="organizationId">The organization ID to get policy requirements for</param>
    /// <returns>The policy requirement of type T</returns>
    Task<T> GetAsync<T>(Guid organizationId) where T : IPolicyRequirement;
}
