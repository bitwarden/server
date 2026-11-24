using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.PushRegistration;
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

    public async Task SaveAsync(WebPushRegistrationData webPush, Device device, IEnumerable<string> organizationIds)
    {
        await _pushRegistrationService.CreateOrUpdateRegistrationAsync(
            new PushRegistrationData(webPush.Endpoint, webPush.P256dh, webPush.Auth),
            device.Id.ToString(),
            device.UserId.ToString(),
            device.Identifier,
            device.Type,
            organizationIds,
            _globalSettings.Installation.Id
        );
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
        device.EncryptedPrivateKey = null;
        device.EncryptedPublicKey = null;
        device.EncryptedUserKey = null;
        await _deviceRepository.UpsertAsync(device);

        await _pushRegistrationService.DeleteRegistrationAsync(device.Id.ToString());
    }
}
