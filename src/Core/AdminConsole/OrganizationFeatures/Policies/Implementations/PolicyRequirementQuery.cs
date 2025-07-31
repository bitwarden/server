#nullable enable

using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery(
    IPolicyRepository policyRepository,
    IEnumerable<IPolicyRequirementFactory> policyRequirementFactories)
    : IPolicyRequirementQuery
{
    public async Task<T> GetRequirementAsync<T>(Guid organizationUserId) where T : ISinglePolicyRequirement
    {
        var policyDetails = new PolicyDetails(); // TODO: imagine this gets PolicyDetails by OrganizationUserId - for that user and that org only
        var factory = policyRequirementFactories.OfType<ISinglePolicyRequirementFactory<T>>().Single();

        return IsExempt(factory, policyDetails, filterStatus: true)
            ? factory.Create()
            : factory.Create(policyDetails);
    }

    // TODO: bulk implementation of GetRequirementAsync
    public Task<Dictionary<Guid, T>> GetRequirementAsync<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement => throw new NotImplementedException();

    public async Task<T> GetAggregateRequirement<T>(Guid userId) where T : IAggregatePolicyRequirement
    {
        // This is basically the method we already have today
        var policyDetails = await policyRepository.GetPolicyDetailsByUserId(userId);
        var factory = policyRequirementFactories.OfType<IAggregatePolicyRequirementFactory<T>>().Single();

        var filtered = policyDetails.Where(p => !IsExempt(factory, p, filterStatus: true));
        return factory.Create(filtered);
    }

    public async Task<T> GetPreAccessRequirement<T>(Guid organizationUserId) where T : ISinglePolicyRequirement
    {
        var policyDetails = new PolicyDetails(); // TODO: imagine this gets PolicyDetails by OrganizationUserId - for that user and that org only
        var factory = policyRequirementFactories.OfType<ISinglePolicyRequirementFactory<T>>().Single();

        // This is basically the same as GetRequirementAsync, except that it does NOT check status
        return IsExempt(factory, policyDetails, filterStatus: false)
            ? factory.Create()
            : factory.Create(policyDetails);
    }

    // TODO: bulk implementation
    public Task<Dictionary<Guid, T>> GetPreAccessRequirement<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement => throw new NotImplementedException();

    /// <summary>
    /// Internal helper method to decide whether a policy should apply to a user, based on status, provider relationship, and role.
    /// </summary>
    private static bool IsExempt(IPolicyRequirementFactory factory, PolicyDetails policyDetails, bool filterStatus)
    {
        var isRoleExempt = factory.ExemptRoles(policyDetails.OrganizationUserType) ||
                           (factory.ExemptProviders && policyDetails.IsProvider);

        if (isRoleExempt || !filterStatus)
        {
            return isRoleExempt;
        }

        var isStatusExempt =
            (!factory.EnforceInAcceptedStatus && policyDetails.OrganizationUserStatus == OrganizationUserStatusType.Accepted) ||
            policyDetails.OrganizationUserStatus is OrganizationUserStatusType.Invited or OrganizationUserStatusType.Revoked;

        return isStatusExempt;
    }
}
