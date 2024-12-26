using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IDeviceRepository : IRepository<Device, Guid>
{
    Task<Device?> GetByIdAsync(Guid id, Guid userId);
    Task<Device?> GetByIdentifierAsync(string identifier);
    Task<Device?> GetByIdentifierAsync(string identifier, Guid userId);
    Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId);
    // DeviceAuthDetails is passed back to decouple the response model from the
    // repository in case more fields are ever added to the details response for
    // other requests.
    Task<ICollection<DeviceAuthDetails>> GetManyByUserIdWithDeviceAuth(Guid userId);
    Task ClearPushTokenAsync(Guid id);
}
