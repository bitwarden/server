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

    public async Task<Core.Auth.Entities.TwoFactorRememberToken?> GetByUserIdDeviceIdAsync(Guid userId, Guid deviceId)
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
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var existing = await dbContext.TwoFactorRememberTokens
            .FirstOrDefaultAsync(t => t.UserId == token.UserId && t.DeviceId == token.DeviceId);

        if (existing != null)
        {
            // CreationDate is deliberately left alone so the row still records when the device was
            // first remembered, matching the MSSQL procedure.
            existing.Stamp = token.Stamp;
            existing.RevisionDate = token.RevisionDate;
            existing.ExpirationDate = token.ExpirationDate;
            await dbContext.SaveChangesAsync();

            token.Id = existing.Id;
            token.CreationDate = existing.CreationDate;
            return token;
        }

        token.SetNewId();
        var entity = Mapper.Map<Models.TwoFactorRememberToken>(token);
        await dbContext.AddAsync(entity);
        await dbContext.SaveChangesAsync();

        return token;
    }

    public async Task RotateStampsByUserIdAsync(Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // One stamp shared across the user's rows, generated here rather than per row, so this and
        // the MSSQL procedure write identical values. Rows are located by (UserId, DeviceId), so the
        // stamp never selects a row and a token naming one device cannot match another's.
        var stamp = Guid.NewGuid().ToString();
        var revisionDate = DateTime.UtcNow;

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
