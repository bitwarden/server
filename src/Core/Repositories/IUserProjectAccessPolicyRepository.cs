using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IUserProjectAccessPolicyRepository
{
    Task<UserProjectAccessPolicy> GetByIdAsync(Guid id);
    Task<UserProjectAccessPolicy> CreateAsync(UserProjectAccessPolicy obj);
    Task ReplaceAsync(UserProjectAccessPolicy obj);
    Task DeleteAsync(UserProjectAccessPolicy obj);
}
