using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Infrastructure.Dapper.Repositories;

public class InstallationRepository : Repository<Installation, Guid>, IInstallationRepository
{
    public InstallationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public InstallationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }
}
