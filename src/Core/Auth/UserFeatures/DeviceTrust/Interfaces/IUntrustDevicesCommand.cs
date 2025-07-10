using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.DeviceTrust;

public interface IUntrustDevicesCommand
{
    public Task UntrustDevices(User user, IEnumerable<Guid> devicesToUntrust);
}
