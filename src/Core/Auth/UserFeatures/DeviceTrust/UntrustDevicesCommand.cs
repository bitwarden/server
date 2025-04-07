using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.DeviceTrust;

public class UntrustDevicesCommand : IUntrustDevicesCommand
{
    private readonly IDeviceRepository _deviceRepository;

    public UntrustDevicesCommand(
        IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task UntrustDevices(User user, IEnumerable<Guid> devicesToUntrust)
    {
        var userDevices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        foreach (var deviceId in devicesToUntrust)
        {
            if (!userDevices.Any(d => d.Id == deviceId))
            {
                throw new UnauthorizedAccessException($"User {user.Id} does not have access to device {deviceId}");
            }
        }

        foreach (var deviceId in devicesToUntrust)
        {
            var device = userDevices.First(d => d.Id == deviceId);
            device.EncryptedPrivateKey = null;
            device.EncryptedPublicKey = null;
            device.EncryptedUserKey = null;
            await _deviceRepository.UpsertAsync(device);
        }
    }
}
