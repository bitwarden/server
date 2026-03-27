using AutoMapper;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories;

public class UserPreferencesRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
    : Repository<Core.Vault.Entities.UserPreferences, UserPreferences, Guid>(serviceScopeFactory, mapper,
        (DatabaseContext context) => context.UserPreferences), IUserPreferencesRepository
{
    public async Task<Core.Vault.Entities.UserPreferences?> GetByUserIdAsync(Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var result = await dbContext.UserPreferences
            .FirstOrDefaultAsync(up => up.UserId == userId);

        return Mapper.Map<Core.Vault.Entities.UserPreferences?>(result);
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await dbContext.UserPreferences
            .Where(up => up.UserId == userId)
            .ExecuteDeleteAsync();
    }
}
