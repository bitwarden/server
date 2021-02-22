using System;
using Bit.Core.Models.Table;
using Bit.Core.Settings;

namespace Bit.Core.Repositories.SqlServer
{
    public class InstallationRepository : Repository<Installation, Guid>, IInstallationRepository
    {
        public InstallationRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public InstallationRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }
    }
}
