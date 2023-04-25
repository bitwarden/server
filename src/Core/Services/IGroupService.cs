using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Services;

public interface IGroupService
{
    [Obsolete("IDeleteGroupCommand should be used instead. To be removed by EC-608.")]
    Task DeleteAsync(Group group);
    [Obsolete("IDeleteGroupCommand should be used instead. To be removed by EC-608.")]
    Task DeleteAsync(Group group, EventSystemUser systemUser);
    Task DeleteUserAsync(GroupUser groupUser);
    Task DeleteUserAsync(GroupUser groupUser, EventSystemUser systemUser);
}
