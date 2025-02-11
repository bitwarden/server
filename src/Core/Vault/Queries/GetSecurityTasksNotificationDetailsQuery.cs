using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Queries;

public class GetSecurityTasksNotificationDetailsQuery : IGetSecurityTasksNotificationDetailsQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly ICipherRepository _cipherRepository;

    public GetSecurityTasksNotificationDetailsQuery(ICurrentContext currentContext, ICipherRepository cipherRepository)
    {
        _currentContext = currentContext;
        _cipherRepository = cipherRepository;
    }

    public async Task<ICollection<UserSecurityTaskCipher>> GetNotificationDetailsByManyIds(Guid organizationId, IEnumerable<SecurityTask> tasks)
    {
        var org = _currentContext.GetOrganization(organizationId);
        var cipherIds = tasks
            .Where(task => task.CipherId.HasValue)
            .Select(task => task.CipherId.Value)
            .ToList();

        if (org == null)
        {
            throw new NotFoundException();
        }

        var userSecurityTaskCiphers = await _cipherRepository.GetUserSecurityTasksByCipherIdsAsync(organizationId, cipherIds);

        return userSecurityTaskCiphers;
    }
}
