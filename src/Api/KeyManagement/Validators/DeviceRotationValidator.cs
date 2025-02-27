﻿using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Api.KeyManagement.Validators;

/// <summary>
/// Send implementation for <see cref="IRotationValidator{T,R}"/>
/// </summary>
public class DeviceRotationValidator : IRotationValidator<IEnumerable<OtherDeviceKeysUpdateRequestModel>, IEnumerable<Device>>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceService _deviceService;

    /// <summary>
    /// Instantiates a new <see cref="DeviceRotationValidator"/>
    /// </summary>
    /// <param name="deviceService">Retrieves all user <see cref="Device"/>s</param>
    public DeviceRotationValidator(IDeviceRepository deviceRepository, IDeviceService deviceService)
    {
        _deviceRepository = deviceRepository;
        _deviceService = deviceService;
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
                throw new BadRequestException("All existing sends must be included in the rotation.");
            }

            result.Add(device.ToDevice(existing));
        }

        return result;
    }
}
