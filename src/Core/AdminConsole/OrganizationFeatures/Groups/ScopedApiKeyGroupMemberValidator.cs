using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public class ScopedApiKeyGroupMemberValidator(
    ICurrentContext currentContext,
    IGroupRepository groupRepository,
    IOrganizationUserRepository organizationUserRepository) : IScopedApiKeyGroupMemberValidator
{
    public async Task<Error?> ValidateAsync(Guid organizationId, Guid? groupId, IEnumerable<Guid> memberIds)
    {
        if (!currentContext.IsScopedOrganizationApiKey)
        {
            return null;
        }

        var changedMemberIds = groupId.HasValue
            ? new HashSet<Guid>(await groupRepository.GetManyUserIdsByIdAsync(groupId.Value))
            : [];
        changedMemberIds.SymmetricExceptWith(memberIds);
        if (changedMemberIds.Count == 0)
        {
            return null;
        }

        var changedMembers = await organizationUserRepository.GetManyAsync(changedMemberIds);
        return changedMembers.Any(m => m.OrganizationId == organizationId && m.Type != OrganizationUserType.User)
            ? new ScopedApiKeyCanOnlyManageUsers()
            : null;
    }
}
