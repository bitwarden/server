using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IGroupService
{
    Task SaveAsync(Group group, IEnumerable<SelectionReadOnly> collections = null);
    [Obsolete("IDeleteGroupCommand should be used instead. To be removed by EC-608.")]
    Task DeleteAsync(Group group);
    Task DeleteUserAsync(Group group, Guid organizationUserId);
}
