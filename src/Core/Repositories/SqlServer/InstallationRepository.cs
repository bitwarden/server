using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories.SqlServer
{
    public class InstallationRepository : Repository<Installation, Guid>, IInstallationRepository
    {
        public InstallationRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public InstallationRepository(string connectionString)
            : base(connectionString)
        { }
    }
}
