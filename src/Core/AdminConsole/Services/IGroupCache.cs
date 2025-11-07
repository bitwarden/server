using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Services;

public interface IGroupCache
{
    Task<Group?> GetAsync(Guid groupId);
}
