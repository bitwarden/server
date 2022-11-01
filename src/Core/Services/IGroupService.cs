using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IGroupService
{
    Task SaveAsync(Group group, IEnumerable<CollectionAccessSelection> collections = null);
    Task DeleteAsync(Group group);
    Task DeleteUserAsync(Group group, Guid organizationUserId);
}
