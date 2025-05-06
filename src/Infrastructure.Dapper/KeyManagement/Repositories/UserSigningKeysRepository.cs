#nullable enable
using System.Data;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.KeyManagement.Repositories;

public class UserSigningKeysRepository : Repository<UserSigningKeys, Guid>, IUserSigningKeysRepository
{
    public UserSigningKeysRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public UserSigningKeysRepository(string connectionString, string readOnlyConnectionString) : base(
        connectionString, readOnlyConnectionString)
    {
    }

    public async Task<SigningKeyData?> GetByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.QuerySingleOrDefaultAsync<SigningKeyData>(
                "[dbo].[SigningKeys_ReadByUserId]",
                new
                {
                    UserId = userId
                },
                commandType: CommandType.StoredProcedure);
        }
    }

    public UpdateEncryptedDataForKeyRotation SetUserSigningKeys(Guid userId, SigningKeyData signingKeys)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            await using (var cmd = new SqlCommand("[dbo].[UserSigningKeys_SetForRotation]", connection, transaction))
            {
                cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() });
                cmd.Parameters.Add(new SqlParameter("@UserId", SqlDbType.UniqueIdentifier) { Value = userId });
                cmd.Parameters.Add(new SqlParameter("@KeyType", SqlDbType.TinyInt) { Value = (byte)signingKeys.KeyType });
                cmd.Parameters.Add(new SqlParameter("@VerifyingKey", SqlDbType.NVarChar) { Value = signingKeys.VerifyingKey });
                cmd.Parameters.Add(new SqlParameter("@SigningKey", SqlDbType.NVarChar) { Value = signingKeys.WrappedSigningKey });
                cmd.Parameters.Add(new SqlParameter("@CreationDate", SqlDbType.DateTime) { Value = DateTime.UtcNow });
                cmd.Parameters.Add(new SqlParameter("@RevisionDate", SqlDbType.DateTime) { Value = DateTime.UtcNow });
                await cmd.ExecuteNonQueryAsync();
            }
        };
    }

    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SigningKeyData signingKeys)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            await using (var cmd = new SqlCommand("[dbo].[UserSigningKeys_UpdateForRotation]", connection, transaction))
            {
                cmd.Parameters.Add(new SqlParameter("@UserId", SqlDbType.UniqueIdentifier) { Value = grantorId });
                cmd.Parameters.Add(new SqlParameter("@KeyType", SqlDbType.TinyInt) { Value = (byte)signingKeys.KeyType });
                cmd.Parameters.Add(new SqlParameter("@VerifyingKey", SqlDbType.NVarChar) { Value = signingKeys.VerifyingKey });
                cmd.Parameters.Add(new SqlParameter("@SigningKey", SqlDbType.NVarChar) { Value = signingKeys.WrappedSigningKey });
                cmd.Parameters.Add(new SqlParameter("@RevisionDate", SqlDbType.DateTime) { Value = DateTime.UtcNow });
                cmd.ExecuteNonQuery();
            }
        };
    }
}
