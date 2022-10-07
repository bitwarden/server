using Bit.Core.Commands.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Commands;

public class PushDeleteUserRegistrationOrganizationCommand : IPushDeleteUserRegistrationOrganizationCommand
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;

    public PushDeleteUserRegistrationOrganizationCommand(IDeviceRepository deviceRepository, IPushNotificationService pushNotificationService, IPushRegistrationService pushRegistrationService)
    {
        _deviceRepository = deviceRepository;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
    }

    public async Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId)
    {
        var deviceIds = await GetUserDeviceIdsAsync(userId);
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(deviceIds,
            organizationId.ToString());
        await _pushNotificationService.PushSyncOrgKeysAsync(userId);
    }

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
        return devices.Where(d => !string.IsNullOrWhiteSpace(d.PushToken)).Select(d => d.Id.ToString());
    }
}
