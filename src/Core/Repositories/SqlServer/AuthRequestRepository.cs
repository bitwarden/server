using System;
using Bit.Core.Models.Table;
using Bit.Core.Settings;

namespace Bit.Core.Repositories.SqlServer
{
    public class AuthRequestRepository : Repository<AuthRequest, Guid>, IAuthRequestRepository
    {
        public AuthRequestRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public AuthRequestRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }
    }
}
