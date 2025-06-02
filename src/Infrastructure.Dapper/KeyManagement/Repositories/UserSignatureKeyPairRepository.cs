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

public class UserSignatureKeyPairRepository : Repository<UserSignatureKeyPair, Guid>, IUserSignatureKeyPairRepository
{
    public UserSignatureKeyPairRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public UserSignatureKeyPairRepository(string connectionString, string readOnlyConnectionString) : base(
        connectionString, readOnlyConnectionString)
    {
    }

    public async Task<SignatureKeyPairData?> GetByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.QuerySingleOrDefaultAsync<SignatureKeyPairData>(
                "[dbo].[UserSigningKey_ReadByUserId]",
                new
                {
                    UserId = userId
                },
                commandType: CommandType.StoredProcedure);
        }
    }

    public UpdateEncryptedDataForKeyRotation SetUserSignatureKeyPair(Guid userId, SignatureKeyPairData signingKeys)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            await connection.QueryAsync(
                "[dbo].[UserSigningKey_SetForRotation]",
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SignatureAlgorithm = (byte)signingKeys.SignatureAlgorithm,
                    signingKeys.VerifyingKey,
                    SigningKey = signingKeys.WrappedSigningKey,
                    CreationDate = DateTime.UtcNow,
                    RevisionDate = DateTime.UtcNow
                },
                commandType: CommandType.StoredProcedure,
                transaction: transaction);
        };
    }

    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SignatureKeyPairData signingKeys)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            await connection.QueryAsync(
                "[dbo].[UserSigningKey_UpdateForRotation]",
                new
                {
                    UserId = grantorId,
                    SignatureAlgorithm = (byte)signingKeys.SignatureAlgorithm,
                    signingKeys.VerifyingKey,
                    SigningKey = signingKeys.WrappedSigningKey,
                    RevisionDate = DateTime.UtcNow
                },
                commandType: CommandType.StoredProcedure,
                transaction: transaction);
        };
    }
}
