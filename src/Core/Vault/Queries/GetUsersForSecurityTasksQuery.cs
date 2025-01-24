using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Queries;

public class GetUsersForSecurityTasksQuery : IGetUsersForSecurityTasksQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly ICipherRepository _cipherRepository;

    public GetUsersForSecurityTasksQuery(ICurrentContext currentContext, ICipherRepository cipherRepository)
    {
        _currentContext = currentContext;
        _cipherRepository = cipherRepository;
    }

    public async Task<ICollection<UserSecurityTasksCount>> GetAllUsersBySecurityTasks(Guid organizationId, IEnumerable<SecurityTask> tasks)
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

        var userSecurityTasks = await _cipherRepository.GetUserSecurityTasksByCipherIdsAsync(organizationId, cipherIds);

        return userSecurityTasks;
    }
}
