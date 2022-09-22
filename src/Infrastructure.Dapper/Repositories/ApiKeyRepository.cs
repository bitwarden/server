using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Infrastructure.Dapper.Repositories;

public class ApiKeyRepository: Repository<ApiKey, Guid>, IApiKeyRepository
{
    public ApiKeyRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public ApiKeyRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }
}
