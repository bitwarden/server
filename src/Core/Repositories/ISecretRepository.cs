using Bit.Core.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Core.Repositories;

public interface ISecretRepository
{
    Task<IEnumerable<Secret>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType, bool orgAdmin);
    Task<IEnumerable<Secret>> GetManyByIds(IEnumerable<Guid> ids, Guid userId, AccessClientType accessType, bool orgAdmin);
    Task<IEnumerable<Secret>> GetManyByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType, bool orgAdmin);
    Task<Secret> GetByIdAsync(Guid id, Guid userId, AccessClientType accessType, bool orgAdmin);
    Task<Secret> CreateAsync(Secret secret, Guid userId, AccessClientType accessType, bool orgAdmin);
    Task<Secret> UpdateAsync(Secret secret, Guid userId, AccessClientType accessType, bool orgAdmin);
    Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids, Guid userId, AccessClientType accessType, bool orgAdmin);
}
