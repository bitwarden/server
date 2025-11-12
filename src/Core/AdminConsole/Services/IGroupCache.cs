using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Services;

public interface IGroupCache
{
    Task<Group?> GetAsync(Guid groupId);
}
