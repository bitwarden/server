using Bit.Core.Enums;
using Bit.Core.Platform.Push;

namespace Bit.Core.Platform.PushRegistration.Internal;

public class NoopPushRegistrationService : IPushRegistrationService
{
    public Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
    {
        return Task.FromResult(0);
    }

    public Task CreateOrUpdateRegistrationAsync(PushRegistrationData pushRegistrationData, string deviceId, string userId,
        string identifier, DeviceType type, IEnumerable<string> organizationIds, Guid installationId)
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
