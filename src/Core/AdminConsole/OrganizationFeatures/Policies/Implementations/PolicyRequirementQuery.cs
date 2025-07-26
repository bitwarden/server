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

    private Task<IEnumerable<PolicyDetails>> GetPolicyDetails(Guid userId)
        => policyRepository.GetPolicyDetailsByUserId(userId);

    public async Task<T> GetRequirementAsync<T>(Guid organizationUserId) where T : ISinglePolicyRequirement
    {
        var policyDetails = new PolicyDetails(); // TODO: should get by orgUserId
        var factory = policyRequirementFactories.OfType<ISinglePolicyRequirementFactory<T>>().Single();

        return IsExempt(factory, policyDetails, filterStatus: true)
            ? factory.Create()
            : factory.Create(policyDetails);
    }

    public Task<Dictionary<Guid, T>> GetRequirementAsync<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement => throw new NotImplementedException();

    public async Task<T> GetAggregateRequirement<T>(Guid userId) where T : IAggregatePolicyRequirement
    {
        var policyDetails = await policyRepository.GetPolicyDetailsByUserId(userId);
        var factory = policyRequirementFactories.OfType<IAggregatePolicyRequirementFactory<T>>().Single();

        var filtered = policyDetails.Where(p => !IsExempt(factory, p, filterStatus: true));
        return factory.Create(filtered);
    }

    public async Task<T> GetPreAccessRequirement<T>(Guid organizationUserId) where T : ISinglePolicyRequirement
    {
        var policyDetails = await policyRepository.GetPolicyDetailsByUserId(organizationUserId); // TODO; by orguserid
        var factory = policyRequirementFactories.OfType<IAggregatePolicyRequirementFactory<T>>().Single();

        // does NOT check status!
        var filtered = policyDetails.Where(p => !IsExempt(factory, p, filterStatus: false));
        return factory.Create(filtered);
    }

    public Task<Dictionary<Guid, T>> GetPreAccessRequirement<T>(IEnumerable<Guid> organizationUserIds) where T : ISinglePolicyRequirement => throw new NotImplementedException();

    private bool IsExempt(IPolicyRequirementFactory factory, PolicyDetails policyDetails, bool filterStatus)
    {
        var isRoleExempt = factory.ExemptRoles(policyDetails.OrganizationUserType) ||
                           (factory.ExemptProviders && policyDetails.IsProvider);

        if (isRoleExempt || !filterStatus)
        {
            return isRoleExempt;
        }

        var isStatusExempt =
            (!factory.EnforceWhenAccepted && policyDetails.OrganizationUserStatus == OrganizationUserStatusType.Accepted) ||
            policyDetails.OrganizationUserStatus is OrganizationUserStatusType.Invited or OrganizationUserStatusType.Revoked;

        return isStatusExempt;
    }
}
