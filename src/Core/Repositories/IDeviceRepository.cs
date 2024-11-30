using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IDeviceRepository : IRepository<Device, Guid>
{
    Task<Device?> GetByIdAsync(Guid id, Guid userId);
    Task<Device?> GetByIdentifierAsync(string identifier);
    Task<Device?> GetByIdentifierAsync(string identifier, Guid userId);
    Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId);
    Task<ICollection<Device>> GetManyByUserIdWithDeviceAuth(Guid userId, int expirationMinutes);
    Task ClearPushTokenAsync(Guid id);
}
