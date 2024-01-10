using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;

public class SameOrganizationQuery : ISameOrganizationQuery
{
    private readonly IGroupRepository _groupRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public SameOrganizationQuery(IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository, IServiceAccountRepository serviceAccountRepository)
    {
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<bool> OrgUsersInTheSameOrgAsync(List<Guid> organizationUserIds, Guid organizationId)
    {
        var users = await _organizationUserRepository.GetManyAsync(organizationUserIds);
        return users.All(user => user.OrganizationId == organizationId) &&
               users.Count == organizationUserIds.Count;
    }

    public async Task<bool> GroupsInTheSameOrgAsync(List<Guid> groupIds, Guid organizationId)
    {
        var groups = await _groupRepository.GetManyByManyIds(groupIds);
        return groups.All(group => group.OrganizationId == organizationId) &&
               groups.Count == groupIds.Count;
    }

    public async Task<bool> ServiceAccountsInTheSameOrgAsync(List<Guid> serviceAccountIds, Guid organizationId)
    {
        var serviceAccounts = (await _serviceAccountRepository.GetManyByIds(serviceAccountIds)).ToList();
        return serviceAccounts.All(serviceAccount => serviceAccount.OrganizationId == organizationId) &&
               serviceAccounts.Count == serviceAccountIds.Count;
    }
}
