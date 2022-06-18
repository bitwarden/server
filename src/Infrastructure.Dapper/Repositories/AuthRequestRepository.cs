using System;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Infrastructure.Dapper.Repositories
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
