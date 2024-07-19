using Bit.Core.Enums;
using Bit.Core.NotificationHub;

namespace Bit.Core.Services;

public interface IPushRegistrationService
{
    Task CreateOrUpdateRegistrationAsync(PushRegistrationData data, string deviceId, string userId, string identifier, DeviceType type);
    Task DeleteRegistrationAsync(string deviceId);
    Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId);
    Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId);
}
