using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IGroupService
{
    Task SaveAsync(Group group, IEnumerable<SelectionReadOnly> collections = null, EventSystemUser? systemUser = null);
    Task DeleteAsync(Group group, EventSystemUser? systemUser = null);
    Task DeleteUserAsync(Group group, Guid organizationUserId, EventSystemUser? systemUser = null);
}
