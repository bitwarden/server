using AutoMapper;
using Bit.Core.Auth.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class TwoFactorRememberTokenRepository : BaseEntityFrameworkRepository, ITwoFactorRememberTokenRepository
{
    public TwoFactorRememberTokenRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    // Virtual so tests can simulate a stale read, which is what the fallbacks in UpsertAsync handle.
    public virtual async Task<Core.Auth.Entities.TwoFactorRememberToken?> GetByUserIdDeviceIdAsync(
        Guid userId, Guid deviceId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        return await dbContext.TwoFactorRememberTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.DeviceId == deviceId);
    }

    public async Task<Core.Auth.Entities.TwoFactorRememberToken> UpsertAsync(
        Core.Auth.Entities.TwoFactorRememberToken token)
    {
        var existing = await GetByUserIdDeviceIdAsync(token.UserId, token.DeviceId);
        if (existing != null && await TryUpdateAsync(token, existing))
        {
            return token;
        }

        // Either there is no row, or it was removed between the read and the update (the expiry
        // sweep can do that).
        token.SetNewId();

        try
        {
            await CreateAsync(token);
        }
        catch (DbUpdateException e) when (IsDuplicateKeyException(e))
        {
            // Unique-index backstop: a concurrent remember-login on the same device inserted the row
            // between our read and our insert. Update the winner's row instead of failing the login.
            existing = await GetByUserIdDeviceIdAsync(token.UserId, token.DeviceId);
            if (existing == null || !await TryUpdateAsync(token, existing))
            {
                throw;
            }
        }

        return token;
    }

    private async Task<bool> TryUpdateAsync(
        Core.Auth.Entities.TwoFactorRememberToken token, Core.Auth.Entities.TwoFactorRememberToken existing)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var rowsUpdated = await dbContext.TwoFactorRememberTokens
            .Where(t => t.Id == existing.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Stamp, token.Stamp)
                .SetProperty(t => t.RevisionDate, token.RevisionDate)
                .SetProperty(t => t.ExpirationDate, token.ExpirationDate));

        if (rowsUpdated == 0)
        {
            return false;
        }

        // CreationDate is left alone so the row still records when the device was first remembered,
        // matching the MSSQL procedure.
        token.Id = existing.Id;
        token.CreationDate = existing.CreationDate;
        return true;
    }

    private async Task CreateAsync(Core.Auth.Entities.TwoFactorRememberToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var entity = Mapper.Map<Models.TwoFactorRememberToken>(token);
        await dbContext.AddAsync(entity);
        await dbContext.SaveChangesAsync();
    }

    /// <remarks>
    /// Recognises unique-<em>index</em> violations, which report different codes from primary-key ones on
    /// SQL Server (2601 rather than 2627) and SQLite (2067 rather than 1555).
    /// </remarks>
    private static bool IsDuplicateKeyException(DbUpdateException e) => e.InnerException switch
    {
        MySqlConnector.MySqlException my => my.ErrorCode == MySqlConnector.MySqlErrorCode.DuplicateKeyEntry,
        Microsoft.Data.SqlClient.SqlException ms => ms.Errors
            .Cast<Microsoft.Data.SqlClient.SqlError>()
            .Any(error => error.Number is 2601 or 2627),
        Npgsql.PostgresException pg => pg.SqlState == "23505",
        Microsoft.Data.Sqlite.SqliteException lite => lite.SqliteErrorCode == 19
            && lite.SqliteExtendedErrorCode is 1555 or 2067,
        _ => false,
    };

    public async Task RotateStampsByUserIdAsync(Guid userId, DateTime revisionDate)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // One stamp shared across the user's rows, generated here rather than per row, so this and
        // the MSSQL procedure write identical values. Rows are located by (UserId, DeviceId), so the
        // stamp never selects a row and a token naming one device cannot match another's.
        var stamp = Guid.NewGuid().ToString();

        await dbContext.TwoFactorRememberTokens
            .Where(t => t.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Stamp, stamp)
                .SetProperty(t => t.RevisionDate, revisionDate));
    }

    public async Task DeleteExpiredAsync(DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await dbContext.TwoFactorRememberTokens
            .Where(t => t.ExpirationDate < now)
            .ExecuteDeleteAsync();
    }
}
