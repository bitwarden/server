namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    /// <summary>
    /// Get a Policy Requirement specific to a particular OrganizationUser and organization.
    /// This
    /// </summary>
    /// <remarks>
    /// This will not take into account any policies from other organizations that may affect the user.
    /// Only use this when you want to limit your check to a specific organization's policy.
    /// </remarks>
    Task<T> GetRequirementAsync<T>(Guid organizationUserId) where T : ISinglePolicyRequirement<T>;
    Task<Dictionary<Guid, T>> GetRequirementAsync<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement<T>;

    /// <summary>
    /// Get an aggregated Policy Requirement, which combines all policies that apply to the user
    /// in all organizations they are a member of.
    /// </summary>
    /// <remarks>
    /// This is often used when checking a user action for compliance with any applicable policies, and you are not
    /// concerned with any particular organization.
    /// </remarks>
    Task<T> GetAggregateRequirement<T>(Guid userId) where T : IAggregatePolicyRequirement<T>;

    /// <summary>
    /// Get a Policy Requirement specific to a particular OrganizationUser and organization,
    /// before the user is given access to organization resources by being accepted, confirmed, or restored.
    /// </summary>
    Task<T> GetPreAccessRequirement<T>(Guid organizationUserId) where T : ISinglePolicyRequirement<T>;
    Task<Dictionary<Guid, T>> GetPreAccessRequirement<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement<T>;
}
