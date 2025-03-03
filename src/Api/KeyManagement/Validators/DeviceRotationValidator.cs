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

        var existingDevices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        if (existingDevices == null || existingDevices.Count == 0)
        {
            return result;
        }

        foreach (var existing in existingDevices)
        {
            var device = devices.FirstOrDefault(c => c.DeviceId == existing.Id);
            if (device == null)
            {
                throw new BadRequestException("All existing devices must be included in the rotation.");
            }

            if (existing.IsTrusted() && (device.EncryptedUserKey == null || device.EncryptedPublicKey == null))
            {
                throw new BadRequestException("Rotated encryption keys must be provided for all devices that are trusted.");
            }
            else if (!existing.IsTrusted() && (device.EncryptedUserKey != null || device.EncryptedPublicKey != null))
            {
                throw new BadRequestException("Rotated encryption keys must not be provided for devices that are not trusted.");
            }

            result.Add(device.ToDevice(existing));
        }

        return result;
    }
}
