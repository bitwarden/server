namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    /// <summary>
    /// Get a Policy Requirement specific to an organization member and their organization.
    /// </summary>
    /// <remarks>
    /// Use case: enforce a policy of a specific organization against a user who has been accepted or confirmed to that organization.
    /// </remarks>
    Task<T> GetRequirementAsync<T>(Guid organizationUserId) where T : ISinglePolicyRequirement<T>;
    Task<Dictionary<Guid, T>> GetRequirementAsync<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement<T>;

    /// <summary>
    /// Get an aggregated Policy Requirement, which combines all policies that apply to the user
    /// in all organizations they are a member of.
    /// </summary>
    /// <remarks>
    /// Use case: enforce all policies that may affect a user.
    /// </remarks>
    Task<T> GetAggregateRequirement<T>(Guid userId) where T : IAggregatePolicyRequirement<T>;

    /// <summary>
    /// Get a Policy Requirement specific to an organization before a member is allowed to join.
    /// </summary>
    /// <remarks>
    /// Use case: enforce a policy of a specific organization against a user as part of the accept or confirm flows.
    /// </remarks>
    Task<T> GetPreAccessRequirement<T>(Guid organizationUserId) where T : ISinglePolicyRequirement<T>;
    Task<Dictionary<Guid, T>> GetPreAccessRequirement<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement<T>;
}
