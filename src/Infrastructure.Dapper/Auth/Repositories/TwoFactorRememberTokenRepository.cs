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

    // Virtual so tests can simulate a stale read, which is what the fallbacks in UpsertAsync handle.
    public virtual async Task<TwoFactorRememberToken?> GetByUserIdDeviceIdAsync(Guid userId, Guid deviceId)
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
        var existing = await GetByUserIdDeviceIdAsync(token.UserId, token.DeviceId);
        if (existing != null && await TryUpdateAsync(token, existing))
        {
            return token;
        }

        // Either there is no row, or it was removed between the read and the update (the expiry
        // sweep can do that). BaseRepository does not assign ids the way the generic repository does,
        // and the procedure needs one for the insert.
        token.SetNewId();

        try
        {
            await CreateAsync(token);
        }
        catch (SqlException e) when (e.Number is 2601 or 2627)
        {
            // Unique-index backstop ([IX_TwoFactorRememberToken_UserId_DeviceId]): a concurrent
            // remember-login on the same device inserted the row between our read and our insert.
            // Update the winner's row instead of failing the login.
            existing = await GetByUserIdDeviceIdAsync(token.UserId, token.DeviceId);
            if (existing == null || !await TryUpdateAsync(token, existing))
            {
                throw;
            }
        }

        return token;
    }

    private async Task<bool> TryUpdateAsync(TwoFactorRememberToken token, TwoFactorRememberToken existing)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var rowsUpdated = await connection.ExecuteScalarAsync<int>(
            "[dbo].[TwoFactorRememberToken_Update]",
            new
            {
                existing.Id,
                token.Stamp,
                token.RevisionDate,
                token.ExpirationDate,
            },
            commandType: CommandType.StoredProcedure);

        if (rowsUpdated == 0)
        {
            return false;
        }

        // CreationDate stays as first written, so the row still records when the device was first
        // remembered; the update procedure does not write it.
        token.Id = existing.Id;
        token.CreationDate = existing.CreationDate;
        return true;
    }

    private async Task CreateAsync(TwoFactorRememberToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync(
            "[dbo].[TwoFactorRememberToken_Create]",
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
    }

    public async Task RotateStampsByUserIdAsync(Guid userId, DateTime revisionDate)
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
                RevisionDate = revisionDate,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task DeleteExpiredAsync(DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync(
            "[dbo].[TwoFactorRememberToken_DeleteManyExpired]",
            new { Now = now },
            commandType: CommandType.StoredProcedure);
    }
}
