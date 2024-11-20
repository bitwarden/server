using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Queries;

public interface IGetTaskDetailsForUserQuery
{
    Task<IEnumerable<SecurityTask>> GetTaskDetailsForUserAsync(Guid userId, IEnumerable<SecurityTaskStatus> status = null);
}
