using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable


namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class OpaqueKeyExchangeCredentialRepository : Repository<OpaqueKeyExchangeCredential, Guid>, IOpaqueKeyExchangeCredentialRepository
{
    public OpaqueKeyExchangeCredentialRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OpaqueKeyExchangeCredentialRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<OpaqueKeyExchangeCredential?> GetByIdAsync(Guid id, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OpaqueKeyExchangeCredential>(
                $"[{Schema}].[{Table}_ReadByIdUserId]",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<ICollection<OpaqueKeyExchangeCredential>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OpaqueKeyExchangeCredential>(
                $"[{Schema}].[{Table}_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<bool> UpdateAsync(OpaqueKeyExchangeCredential credential)
    {
        using var connection = new SqlConnection(ConnectionString);
        var affectedRows = await connection.ExecuteAsync(
            $"[{Schema}].[{Table}_Update]",
            credential,
            commandType: CommandType.StoredProcedure);

        return affectedRows > 0;
    }

    // TODO - How do we want to handle rotation?
    public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<OpaqueKeyExchangeRotateKeyData> credentials)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            const string sql = @"
                UPDATE WC
                SET
                    WC.[EncryptedPublicKey] = UW.[encryptedPublicKey],
                    WC.[EncryptedUserKey] = UW.[encryptedUserKey]
                FROM
                    [dbo].[WebAuthnCredential] WC
                INNER JOIN
                    OPENJSON(@JsonCredentials)
                    WITH (
                        id UNIQUEIDENTIFIER,
                        encryptedPublicKey NVARCHAR(MAX),
                        encryptedUserKey NVARCHAR(MAX)
                    ) UW
                    ON UW.id = WC.Id
                WHERE
                    WC.[UserId] = @UserId";

            var jsonCredentials = CoreHelpers.ClassToJsonData(credentials);

            await connection.ExecuteAsync(
                sql,
                new { UserId = userId, JsonCredentials = jsonCredentials },
                transaction: transaction,
                commandType: CommandType.Text);
        };
    }

}
