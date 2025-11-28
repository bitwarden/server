using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Queries;

public class GetTaskDetailsForUserQuery(ISecurityTaskRepository securityTaskRepository) : IGetTaskDetailsForUserQuery
{
    /// <inheritdoc />
    public async Task<IEnumerable<SecurityTask>> GetTaskDetailsForUserAsync(Guid userId,
        SecurityTaskStatus? status = null)
        => await securityTaskRepository.GetManyByUserIdStatusAsync(userId, status);
}
