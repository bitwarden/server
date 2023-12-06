using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;


namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class WebAuthnCredentialRepository : Repository<WebAuthnCredential, Guid>, IWebAuthnCredentialRepository
{
    public WebAuthnCredentialRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public WebAuthnCredentialRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<WebAuthnCredential> GetByIdAsync(Guid id, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<WebAuthnCredential>(
                $"[{Schema}].[{Table}_ReadByIdUserId]",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<ICollection<WebAuthnCredential>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<WebAuthnCredential>(
                $"[{Schema}].[{Table}_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
