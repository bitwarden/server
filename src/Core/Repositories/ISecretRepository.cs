using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface ISecretRepository
{
    Task<IEnumerable<Secret>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Secret>> GetManyByIds(IEnumerable<Guid> ids, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Secret>> GetManyByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType);
    Task<Secret> GetByIdAsync(Guid id, Guid userId, AccessClientType accessType);
    Task<Secret> CreateAsync(Secret secret, Guid userId, AccessClientType accessType);
    Task<Secret> UpdateAsync(Secret secret, Guid userId, AccessClientType accessType);
    Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids, Guid userId, AccessClientType accessType);
}
