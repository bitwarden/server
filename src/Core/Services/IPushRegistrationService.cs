using Bit.Core.Enums;

namespace Bit.Core.Services;

public interface IPushRegistrationService
{
    Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
        string identifier, DeviceType type, ApplicationChannel channel);
    Task DeleteRegistrationAsync(string deviceId, ApplicationChannel channel);
    Task AddUserRegistrationOrganizationAsync(IEnumerable<(string deviceId, ApplicationChannel channel)> devices, string organizationId);
    Task DeleteUserRegistrationOrganizationAsync(IEnumerable<(string deviceId, ApplicationChannel channel)> devices, string organizationId);
}
