using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories;

public interface IPolicyRepository : IRepository<Policy, Guid>
{
    Task<Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type);
    Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Policy>> GetManyByUserIdAsync(Guid userId);
    [Obsolete("Use IPolicyService.GetPoliciesApplicableToUserAsync instead.")]
    Task<ICollection<Policy>> GetManyByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
        OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
    [Obsolete("Use IPolicyService.GetPoliciesApplicableToUserAsync instead.")]
    Task<int> GetCountByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
        OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
}
