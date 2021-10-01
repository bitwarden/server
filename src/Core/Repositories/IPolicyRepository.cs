using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;

namespace Bit.Core.Repositories
{
    public interface IPolicyRepository : IRepository<Policy, Guid>
    {
        Task<Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type);
        Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<Policy>> GetManyByUserIdAsync(Guid userId);
        Task<ICollection<Policy>> GetManyByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
            OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
        Task<int> GetCountByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
            OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted);
    }
}
