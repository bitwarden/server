using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IDeviceRepository : IRepository<Device, Guid>
{
    Task<Device?> GetByIdAsync(Guid id, Guid userId);
    Task<Device?> GetByIdentifierAsync(string identifier);
    Task<Device?> GetByIdentifierAsync(string identifier, Guid userId);
    Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId);
    // The response model is passed in here because we lack a good data layer or DTO
    // solution for creating non entity objects that come straight from a join
    // of two tables. If we see this happening again maybe we consider a new approach
    // to how joined data can be transformed coming out of the database and passed up.
    Task<ICollection<DeviceAuthRequestResponseModel>> GetManyByUserIdWithDeviceAuth(Guid userId, int expirationMinutes);
    Task ClearPushTokenAsync(Guid id);
}
