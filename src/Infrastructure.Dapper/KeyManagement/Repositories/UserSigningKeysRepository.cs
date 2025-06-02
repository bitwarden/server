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
                "[dbo].[UserSigningKey_ReadByUserId]",
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
            await connection.QueryAsync(
                "[dbo].[UserSigningKey_SetForRotation]",
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    KeyType = (byte)signingKeys.KeyAlgorithm,
                    signingKeys.VerifyingKey,
                    SigningKey = signingKeys.WrappedSigningKey,
                    CreationDate = DateTime.UtcNow,
                    RevisionDate = DateTime.UtcNow
                },
                commandType: CommandType.StoredProcedure,
                transaction: transaction);
        };
    }

    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SigningKeyData signingKeys)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            await connection.QueryAsync(
                "[dbo].[UserSigningKey_UpdateForRotation]",
                new
                {
                    UserId = grantorId,
                    KeyType = (byte)signingKeys.KeyAlgorithm,
                    signingKeys.VerifyingKey,
                    SigningKey = signingKeys.WrappedSigningKey,
                    RevisionDate = DateTime.UtcNow
                },
                commandType: CommandType.StoredProcedure,
                transaction: transaction);
        };
    }
}
