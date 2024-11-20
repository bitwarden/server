using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Repositories;

public interface ISecurityTaskRepository : IRepository<SecurityTask, Guid>
{
    Task<ICollection<SecurityTask>> GetManyByUserIdAsync(Guid userId, IEnumerable<SecurityTaskStatus> status = null);
}
