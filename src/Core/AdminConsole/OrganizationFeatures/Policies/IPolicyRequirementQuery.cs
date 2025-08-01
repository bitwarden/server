#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    /// <summary>
    /// Get a policy requirement for a specific user.
    /// The policy requirement represents how one or more policy types should be enforced against the user.
    /// It will always return a value even if there are no policies that should be enforced.
    /// This should be used for all policy checks.
    /// </summary>
    /// <param name="userId">The user that you need to enforce the policy against.</param>
    /// <typeparam name="T">The IPolicyRequirement that corresponds to the policy you want to enforce.</typeparam>
    Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement;

    /// <summary>
    /// Get a policy requirement for a specific organization.
    /// This returns the policy requirement that represents the policy state for the entire organization.
    /// It will always return a value even if there are no policies that should be enforced.
    /// This should be used for organization-level policy checks.
    /// </summary>
    /// <param name="organizationId">The organization to check policies for.</param>
    /// <typeparam name="T">The IPolicyRequirement that corresponds to the policy you want to enforce.</typeparam>
    Task<T> GetByOrganizationAsync<T>(Guid organizationId) where T : IPolicyRequirement;
}
