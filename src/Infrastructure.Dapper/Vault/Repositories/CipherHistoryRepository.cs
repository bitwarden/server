// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.Dapper.Vault.Repositories;

public class CipherHistoryRepository : Repository<CipherHistory, Guid>, ICipherHistoryRepository
{
    public CipherHistoryRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public CipherHistoryRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }
}
