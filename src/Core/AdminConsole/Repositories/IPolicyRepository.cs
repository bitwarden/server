using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.AdminConsole.Repositories;

public interface IPolicyRepository : IRepository<Policy, Guid>
{
    /// <summary>
    /// Gets all policies of a given type for an organization.
    /// </summary>
    /// <remarks>
    /// WARNING: do not use this to enforce policies against a user! It returns raw data and does not take into account
    /// various business rules. Use <see cref="IPolicyRequirementQuery"/> instead.
    /// </remarks>
    Task<Policy?> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type);
    Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Policy>> GetManyByUserIdAsync(Guid userId);
    /// <summary>
    /// Gets all PolicyDetails for a user for all policy types.
    /// </summary>
    /// <remarks>
    /// Each PolicyDetail represents an OrganizationUser and a Policy which *may* be enforced
    /// against them. It only returns PolicyDetails for policies that are enabled and where the organization's plan
    /// supports policies. It also excludes "revoked invited" users who are not subject to policy enforcement.
    /// This is consumed by <see cref="IPolicyRequirementQuery"/> to create requirements for specific policy types.
    /// You probably do not want to call it directly.
    /// </remarks>
    Task<IEnumerable<PolicyDetails>> GetPolicyDetailsByUserId(Guid userId);

    /// <summary>
    /// Retrieves <see cref="OrganizationPolicyDetails"/> of the specified <paramref name="policyType"/>
    /// for users in the given organization—and for any other organizations those users belong to.
    /// </summary>
    /// <remarks>
    /// Each PolicyDetail represents an OrganizationUser and a Policy which *may* be enforced
    /// against them. It only returns PolicyDetails for policies that are enabled and where the organization's plan
    /// supports policies. It also excludes "revoked invited" users who are not subject to policy enforcement.
    /// This is consumed by <see cref="IPolicyRequirementQuery"/> to create requirements for specific policy types.
    /// You probably do not want to call it directly.
    /// </remarks>
    Task<IEnumerable<OrganizationPolicyDetails>> GetPolicyDetailsByOrganizationIdAsync(Guid organizationId,
        PolicyType policyType);
}
