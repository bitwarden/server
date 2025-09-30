#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery(
    IPolicyRepository policyRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
    : IPolicyRequirementQuery
{
    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var policyDetails = await GetPolicyDetails(userId);
        var filteredPolicies = policyDetails
            .Where(p => p.PolicyType == factory.PolicyType)
            .Where(factory.Enforce);
        var requirement = factory.Create(filteredPolicies);
        return requirement;
    }

    public async Task<IEnumerable<Guid>> GetManyByOrganizationIdAsync<T>(Guid organizationId)
        where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var organizationPolicyDetails = await GetOrganizationPolicyDetails(organizationId, factory.PolicyType);

        var eligibleOrganizationUserIds = organizationPolicyDetails
            .Where(p => p.PolicyType == factory.PolicyType)
            .Where(factory.Enforce)
            .Select(p => p.OrganizationUserId)
            .ToList();

        return eligibleOrganizationUserIds;
    }

    private Task<IEnumerable<PolicyDetails>> GetPolicyDetails(Guid userId)
        => policyRepository.GetPolicyDetailsByUserId(userId);

    private async Task<IEnumerable<OrganizationPolicyDetails>> GetOrganizationPolicyDetails(Guid organizationId, PolicyType policyType)
        => await policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, policyType);
}

interface IEnforcedPolicy<T>

/// <summary>
/// Represents a combination of multiple organization policies that apply to a user.
/// </summary>
interface IUserEnforcedPolicy<T>
{
    public Guid UserId { get; set; }
    public PolicyType PolicyType { get; set; }
    public bool Enforced { get; set; }
    public T Data { get; set; }
}

/// <summary>
/// Represents a combination of multiple organization policies that apply to a user.
/// </summary>
interface UserEnforcedPolicy<T>
{
    public Guid UserId { get; set; }
    public PolicyType PolicyType { get; set; }
    public bool Enforced { get; set; }
    public T Data { get; set; }
}

interface IEnforcedPolicyStrategy<T>
{
    /// <summary>
    /// The policy type this is for
    /// </summary>
    public PolicyType PolicyType { set; }

    /// <summary>
    /// Whether providers are exempt
    /// </summary>
    public bool ExemptProviders { set; }

    /// <summary>
    /// Roles that should be exempt, if any
    /// </summary>
    public OrganizationUserType[] ExemptRoles { set; }

    /// <summary>
    /// OrganizationUser statuses in which the policy is enforced
    /// </summary>
    public OrganizationUserStatusType[] EnforcedStatuses { set; }

    /// <summary>
    /// A function that combines multiple policies
    /// </summary>
    public Func<Policy, UserEnforcedPolicy<T>> Reducer { get; }
}
