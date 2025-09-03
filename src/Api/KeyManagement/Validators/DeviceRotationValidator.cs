using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Api.KeyManagement.Validators;

/// <summary>
/// Device implementation for <see cref="IRotationValidator{T,R}"/>
/// </summary>
public class DeviceRotationValidator : IRotationValidator<IEnumerable<OtherDeviceKeysUpdateRequestModel>, IEnumerable<Device>>
{
    private readonly IDeviceRepository _deviceRepository;

    /// <summary>
    /// Instantiates a new <see cref="DeviceRotationValidator"/>
    /// </summary>
    /// <param name="deviceRepository">Retrieves all user <see cref="Device"/>s</param>
    public DeviceRotationValidator(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<IEnumerable<Device>> ValidateAsync(User user, IEnumerable<OtherDeviceKeysUpdateRequestModel> devices)
    {
        var result = new List<Device>();

        var existingTrustedDevices = (await _deviceRepository.GetManyByUserIdAsync(user.Id)).Where(d => d.IsTrusted()).ToList();
        if (existingTrustedDevices.Count == 0)
        {
            return result;
        }

        foreach (var existing in existingTrustedDevices)
        {
            var device = devices.FirstOrDefault(c => c.DeviceId == existing.Id);
            if (device == null)
            {
                throw new BadRequestException("All existing trusted devices must be included in the rotation.");
            }

            if (device.EncryptedUserKey == null || device.EncryptedPublicKey == null)
            {
                throw new BadRequestException("Rotated encryption keys must be provided for all devices that are trusted.");
            }

            result.Add(device.ToDevice(existing));
        }

        return result;
    }
}
