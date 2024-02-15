using Bit.Core.Enums;

namespace Bit.Core.Services;

public interface IPushRegistrationService
{
    Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
        string identifier, DeviceType type);
    Task DeleteRegistrationAsync(string deviceId, DeviceType type);
    Task AddUserRegistrationOrganizationAsync(IEnumerable<KeyValuePair<string, DeviceType>> devices, string organizationId);
    Task DeleteUserRegistrationOrganizationAsync(IEnumerable<KeyValuePair<string, DeviceType>> devices, string organizationId);
}
