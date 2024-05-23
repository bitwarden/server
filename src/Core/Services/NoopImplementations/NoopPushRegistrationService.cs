using Bit.Core.Enums;

namespace Bit.Core.Services;

public class NoopPushRegistrationService : IPushRegistrationService
{
    public Task AddUserRegistrationOrganizationAsync(IEnumerable<KeyValuePair<string, DeviceType>> devices, string organizationId)
    {
        return Task.FromResult(0);
    }

    public Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
        string identifier, DeviceType type)
    {
        return Task.FromResult(0);
    }

    public Task DeleteRegistrationAsync(string deviceId, DeviceType deviceType)
    {
        return Task.FromResult(0);
    }

    public Task DeleteUserRegistrationOrganizationAsync(IEnumerable<KeyValuePair<string, DeviceType>> devices, string organizationId)
    {
        return Task.FromResult(0);
    }
}
