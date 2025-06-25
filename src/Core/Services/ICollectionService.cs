using Bit.Core.Entities;

namespace Bit.Core.Services;

public interface ICollectionService
{
    Task DeleteUserAsync(Collection collection, Guid organizationUserId);
}
