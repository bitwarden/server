using Bit.Core.Enums;

namespace Bit.Core.Services;

public class NoopPushRegistrationService : IPushRegistrationService
{
    public Task AddUserRegistrationOrganizationAsync(IEnumerable<(string deviceId, ApplicationChannel channel)> devices, string organizationId)
    {
        return Task.FromResult(0);
    }

    public Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
        string identifier, DeviceType type, ApplicationChannel channel)
    {
        return Task.FromResult(0);
    }

    public Task DeleteRegistrationAsync(string deviceId, ApplicationChannel channel)
    {
        return Task.FromResult(0);
    }

    public Task DeleteUserRegistrationOrganizationAsync(IEnumerable<(string deviceId, ApplicationChannel channel)> devices, string organizationId)
    {
        return Task.FromResult(0);
    }
}
