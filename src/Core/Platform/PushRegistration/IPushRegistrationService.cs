using Bit.Core.Enums;
using Bit.Core.Platform.PushRegistration;

// TODO: Change this namespace to `Bit.Core.Platform.PushRegistration
namespace Bit.Core.Platform.Push;


public interface IPushRegistrationService
{
    Task CreateOrUpdateRegistrationAsync(PushRegistrationData data, string deviceId, string userId, string identifier, DeviceType type, IEnumerable<string> organizationIds, Guid installationId);
    Task DeleteRegistrationAsync(string deviceId);
    Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId);
    Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId);
}
