using System;
using Bit.Core.Models.Table;

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
