using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Queries;

public class GetTaskDetailsForUserQuery : IGetTaskDetailsForUserQuery
{
    public async Task<IEnumerable<SecurityTask>> GetTaskDetailsForUserAsync(Guid userId, IEnumerable<SecurityTaskStatus> status = null) => throw new NotImplementedException();
}
