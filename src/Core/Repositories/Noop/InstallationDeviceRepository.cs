using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.Noop;

public class InstallationDeviceRepository : IInstallationDeviceRepository
{
    public Task UpsertAsync(InstallationDeviceEntity entity)
    {
        return Task.FromResult(0);
    }

    public Task UpsertManyAsync(IList<InstallationDeviceEntity> entities)
    {
        return Task.FromResult(0);
    }

    public Task DeleteAsync(InstallationDeviceEntity entity)
    {
        return Task.FromResult(0);
    }
}
