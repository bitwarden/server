using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IGroupProjectAccessPolicyRepository
{
    Task<GroupProjectAccessPolicy> GetByIdAsync(Guid id);
    Task<GroupProjectAccessPolicy> CreateAsync(GroupProjectAccessPolicy obj);
    Task ReplaceAsync(GroupProjectAccessPolicy obj);
    Task DeleteAsync(GroupProjectAccessPolicy obj);
}
