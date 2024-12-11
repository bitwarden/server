using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Services;

public interface IGroupService
{
    [Obsolete("IDeleteGroupCommand should be used instead. To be removed by EC-608.")]
    Task DeleteAsync(Group group);

    [Obsolete("IDeleteGroupCommand should be used instead. To be removed by EC-608.")]
    Task DeleteAsync(Group group, EventSystemUser systemUser);
    Task DeleteUserAsync(Group group, Guid organizationUserId);
    Task DeleteUserAsync(Group group, Guid organizationUserId, EventSystemUser systemUser);
}
