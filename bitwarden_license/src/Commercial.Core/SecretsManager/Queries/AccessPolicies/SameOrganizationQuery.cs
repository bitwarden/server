using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;

namespace Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;

public class SameOrganizationQuery : ISameOrganizationQuery
{
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public SameOrganizationQuery(IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository)
    {
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
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
}
