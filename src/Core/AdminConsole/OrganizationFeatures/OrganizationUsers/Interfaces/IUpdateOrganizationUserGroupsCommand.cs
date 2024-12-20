using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IUpdateOrganizationUserGroupsCommand
{
    Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds);
}
