using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

/// <summary>
/// Derives from <see cref="BaseRepository"/> rather than the generic repository because every
/// operation is a named procedure; none of them map onto generic CRUD.
/// </summary>
public class TwoFactorRememberTokenRepository : BaseRepository, ITwoFactorRememberTokenRepository
{
    public TwoFactorRememberTokenRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public TwoFactorRememberTokenRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<TwoFactorRememberToken?> GetByUserIdDeviceIdAsync(Guid userId, Guid deviceId)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<TwoFactorRememberToken>(
            "[dbo].[TwoFactorRememberToken_ReadByUserIdDeviceId]",
            new { UserId = userId, DeviceId = deviceId },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task<TwoFactorRememberToken> UpsertAsync(TwoFactorRememberToken token)
    {
        // BaseRepository does not assign ids the way the generic repository does, and the procedure
        // needs one for the insert branch. SetNewId only fills an unset id, so an update is unaffected.
        token.SetNewId();

        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync(
            "[dbo].[TwoFactorRememberToken_Save]",
            new
            {
                token.Id,
                token.UserId,
                token.DeviceId,
                token.Stamp,
                token.CreationDate,
                token.RevisionDate,
                token.ExpirationDate,
            },
            commandType: CommandType.StoredProcedure);

        return token;
    }

    public async Task RotateStampsByUserIdAsync(Guid userId)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync(
            "[dbo].[TwoFactorRememberToken_UpdateManyStampsByUserId]",
            new
            {
                UserId = userId,
                // Generated here rather than with NEWID() so that this and the Entity Framework
                // implementations write the same shape of value. One stamp covers every row for the
                // user; see the procedure for why that is safe.
                Stamp = Guid.NewGuid().ToString(),
                RevisionDate = DateTime.UtcNow,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task DeleteExpiredAsync(DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync(
            "[dbo].[TwoFactorRememberToken_DeleteExpired]",
            new { Now = now },
            commandType: CommandType.StoredProcedure);
    }
}
