using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IServiceAccountProjectAccessPolicyRepository
{
    Task<ServiceAccountProjectAccessPolicy> GetByIdAsync(Guid id);
    Task<ServiceAccountProjectAccessPolicy> CreateAsync(ServiceAccountProjectAccessPolicy obj);
    Task ReplaceAsync(ServiceAccountProjectAccessPolicy obj);
    Task DeleteAsync(ServiceAccountProjectAccessPolicy obj);
}
