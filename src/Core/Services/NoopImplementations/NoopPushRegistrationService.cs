using Bit.Core.Enums;
using Bit.Core.NotificationHub;

namespace Bit.Core.Services;

public class NoopPushRegistrationService : IPushRegistrationService
{
    public Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
    {
        return Task.FromResult(0);
    }

    public Task CreateOrUpdateRegistrationAsync(PushRegistrationData pushRegistrationData, string deviceId, string userId,
        string identifier, DeviceType type)
    {
        return Task.FromResult(0);
    }

    public Task DeleteRegistrationAsync(string deviceId)
    {
        return Task.FromResult(0);
    }

    public Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
    {
        return Task.FromResult(0);
    }
}
