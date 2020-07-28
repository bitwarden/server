using Bit.Core.Models.Table;

namespace Bit.Core.Repositories.SqlServer
{
    public class SsoUserRepository : Repository<SsoUser, long>, ISsoUserRepository
    {
        public SsoUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public SsoUserRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }
    }
}
