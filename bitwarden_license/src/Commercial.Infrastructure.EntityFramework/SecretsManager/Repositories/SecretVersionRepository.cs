using AutoMapper;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class SecretVersionRepository : Repository<Core.SecretsManager.Entities.SecretVersion, SecretVersion, Guid>, ISecretVersionRepository
{
    public SecretVersionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.SecretVersion)
    { }

    public override async Task<Core.SecretsManager.Entities.SecretVersion?> GetByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var secretVersion = await dbContext.SecretVersion
            .Where(sv => sv.Id == id)
            .FirstOrDefaultAsync();
        return Mapper.Map<Core.SecretsManager.Entities.SecretVersion>(secretVersion);
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.SecretVersion>> GetManyBySecretIdAsync(Guid secretId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var secretVersions = await dbContext.SecretVersion
            .Where(sv => sv.SecretId == secretId)
            .OrderByDescending(sv => sv.VersionDate)
            .ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.SecretVersion>>(secretVersions);
    }

    public override async Task<Core.SecretsManager.Entities.SecretVersion> CreateAsync(Core.SecretsManager.Entities.SecretVersion secretVersion)
    {
        const int maxVersionsToKeep = 10;

        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // Get the IDs of the most recent (maxVersionsToKeep - 1) versions to keep
        var versionsToKeepIds = await dbContext.SecretVersion
            .Where(sv => sv.SecretId == secretVersion.SecretId)
            .OrderByDescending(sv => sv.VersionDate)
            .Take(maxVersionsToKeep - 1)
            .Select(sv => sv.Id)
            .ToListAsync();

        // Delete all versions for this secret that are not in the "keep" list
        if (versionsToKeepIds.Any())
        {
            await dbContext.SecretVersion
                .Where(sv => sv.SecretId == secretVersion.SecretId && !versionsToKeepIds.Contains(sv.Id))
                .ExecuteDeleteAsync();
        }

        secretVersion.SetNewId();
        var entity = Mapper.Map<SecretVersion>(secretVersion);

        await dbContext.AddAsync(entity);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return secretVersion;
    }

    public async Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var secretVersionIds = ids.ToList();
        await dbContext.SecretVersion
            .Where(sv => secretVersionIds.Contains(sv.Id))
            .ExecuteDeleteAsync();
    }
}
