using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey;

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
    UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<Device> devices);
    /// <summary>
    /// Updates the device's last-activity state: moves <c>LastActivityDate</c> to today (if not
    /// already today) and writes <paramref name="clientVersion"/> to <c>ClientVersion</c> (if
    /// non-null and different from the stored value). Performs a single DB round trip.
    /// See <see cref="Bit.Core.Auth.UserFeatures.Devices.Interfaces.IUpdateDeviceLastActivityCommand"/>
    /// for why <c>ClientVersion</c> belongs under "last activity" and how this surface extends to
    /// additional last-observed properties.
    /// </summary>
    Task UpdateLastActivityByIdAsync(Guid deviceId, string? clientVersion);

    /// <summary>
    /// Like <see cref="UpdateLastActivityByIdAsync"/>, but locates the device by
    /// (<paramref name="identifier"/>, <paramref name="userId"/>).
    /// </summary>
    Task UpdateLastActivityByIdentifierAndUserIdAsync(string identifier, Guid userId, string? clientVersion);
}
