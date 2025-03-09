﻿using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.NotificationHub;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGlobalSettings _globalSettings;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IPushRegistrationService pushRegistrationService,
        IOrganizationUserRepository organizationUserRepository,
        IGlobalSettings globalSettings)
    {
        _deviceRepository = deviceRepository;
        _pushRegistrationService = pushRegistrationService;
        _organizationUserRepository = organizationUserRepository;
        _globalSettings = globalSettings;
    }

    public async Task SaveAsync(WebPushRegistrationData webPush, Device device)
    {
        await SaveAsync(new PushRegistrationData(webPush.Endpoint, webPush.P256dh, webPush.Auth), device);
    }

    public async Task SaveAsync(Device device)
    {
        await SaveAsync(new PushRegistrationData(device.PushToken), device);
    }

    private async Task SaveAsync(PushRegistrationData data, Device device)
    {
        if (device.Id == default)
        {
            await _deviceRepository.CreateAsync(device);
        }
        else
        {
            device.RevisionDate = DateTime.UtcNow;
            await _deviceRepository.ReplaceAsync(device);
        }

        var organizationIdsString =
            (await _organizationUserRepository.GetManyDetailsByUserAsync(device.UserId,
                OrganizationUserStatusType.Confirmed))
            .Select(ou => ou.OrganizationId.ToString());

        await _pushRegistrationService.CreateOrUpdateRegistrationAsync(data, device.Id.ToString(),
            device.UserId.ToString(), device.Identifier, device.Type, organizationIdsString, _globalSettings.Installation.Id);

    }

    public async Task ClearTokenAsync(Device device)
    {
        await _deviceRepository.ClearPushTokenAsync(device.Id);
        await _pushRegistrationService.DeleteRegistrationAsync(device.Id.ToString());
    }

    public async Task DeactivateAsync(Device device)
    {
        // already deactivated
        if (!device.Active)
        {
            return;
        }

        device.Active = false;
        device.RevisionDate = DateTime.UtcNow;
        await _deviceRepository.UpsertAsync(device);

        await _pushRegistrationService.DeleteRegistrationAsync(device.Id.ToString());
    }

    public async Task UpdateDevicesTrustAsync(string currentDeviceIdentifier,
        Guid currentUserId,
        DeviceKeysUpdateRequestModel currentDeviceUpdate,
        IEnumerable<OtherDeviceKeysUpdateRequestModel> alteredDevices)
    {
        var existingDevices = await _deviceRepository.GetManyByUserIdAsync(currentUserId);

        var currentDevice = existingDevices.FirstOrDefault(d => d.Identifier == currentDeviceIdentifier);

        if (currentDevice == null)
        {
            throw new NotFoundException();
        }

        existingDevices.Remove(currentDevice);

        var alterDeviceKeysDict = alteredDevices.ToDictionary(d => d.DeviceId);

        if (alterDeviceKeysDict.ContainsKey(currentDevice.Id))
        {
            throw new BadRequestException("Current device can not be an optional rotation.");
        }

        currentDevice.EncryptedPublicKey = currentDeviceUpdate.EncryptedPublicKey;
        currentDevice.EncryptedUserKey = currentDeviceUpdate.EncryptedUserKey;

        await _deviceRepository.UpsertAsync(currentDevice);

        foreach (var device in existingDevices)
        {
            if (!device.IsTrusted())
            {
                // You can't update the trust of a device that isn't trusted to begin with
                // should we throw and consider this a BadRequest? If we want to consider it a invalid request
                // we need to check that information before we enter this foreach, we don't want to partially complete
                // this process.
                continue;
            }

            if (alterDeviceKeysDict.TryGetValue(device.Id, out var updateRequest))
            {
                // An update to this device was requested
                device.EncryptedPublicKey = updateRequest.EncryptedPublicKey;
                device.EncryptedUserKey = updateRequest.EncryptedUserKey;
            }
            else
            {
                // No update to this device requested, just untrust it
                device.EncryptedUserKey = null;
                device.EncryptedPublicKey = null;
                device.EncryptedPrivateKey = null;
            }

            await _deviceRepository.UpsertAsync(device);
        }
    }
}
