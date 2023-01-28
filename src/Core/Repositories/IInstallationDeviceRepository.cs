using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IInstallationDeviceRepository
{
    Task UpsertAsync(InstallationDeviceEntity entity);
    Task UpsertManyAsync(IList<InstallationDeviceEntity> entities);
    Task DeleteAsync(InstallationDeviceEntity entity);
}
