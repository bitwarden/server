using System.Text.Json;
using AutoMapper;
using Bit.Core.Platform.Data;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using LinqToDB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using User = Bit.Core.Entities.User;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public abstract class BaseEntityFrameworkRepository
{
    protected BulkCopyOptions DefaultBulkCopyOptions { get; set; } = new BulkCopyOptions
    {
        KeepIdentity = true,
        BulkCopyType = BulkCopyType.MultipleRows,
    };

    public BaseEntityFrameworkRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
    {
        ServiceScopeFactory = serviceScopeFactory;
        Mapper = mapper;
    }

    protected IServiceScopeFactory ServiceScopeFactory { get; private set; }
    protected IMapper Mapper { get; private set; }

    public DatabaseContext GetDatabaseContext(IServiceScope serviceScope)
    {
        return serviceScope.ServiceProvider.GetRequiredService<DatabaseContext>();
    }

    /// <summary>
    /// Returns the ambient DatabaseContext if a transaction is active, or creates a new
    /// scope and resolves a fresh DatabaseContext. The caller must dispose the returned
    /// scope only if it is non-null (i.e., when not using the ambient context).
    /// </summary>
    protected (DatabaseContext DbContext, IServiceScope? OwnedScope) GetDatabaseContextOrAmbient()
    {
        var holder = TransactionState.Current;
        if (holder?.DbContext is DatabaseContext ambientContext)
        {
            return (ambientContext, null);
        }

        var scope = ServiceScopeFactory.CreateScope();
        return (GetDatabaseContext(scope), scope);
    }

    /// <summary>
    /// Executes an action using the ambient transaction's DatabaseContext (if active) or a
    /// new scoped DatabaseContext. The scope is disposed automatically when owned.
    /// </summary>
    protected async Task<TResult> ExecuteWithContextAsync<TResult>(
        Func<DatabaseContext, Task<TResult>> action)
    {
        var (dbContext, ownedScope) = GetDatabaseContextOrAmbient();
        try
        {
            return await action(dbContext);
        }
        finally
        {
            ownedScope?.Dispose();
        }
    }

    /// <summary>
    /// Executes an action using the ambient transaction's DatabaseContext (if active) or a
    /// new scoped DatabaseContext. The scope is disposed automatically when owned.
    /// </summary>
    protected async Task ExecuteWithContextAsync(Func<DatabaseContext, Task> action)
    {
        var (dbContext, ownedScope) = GetDatabaseContextOrAmbient();
        try
        {
            await action(dbContext);
        }
        finally
        {
            ownedScope?.Dispose();
        }
    }

    public void ClearChangeTracking()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            dbContext.ChangeTracker.Clear();
        }
    }

    public async Task<int> GetCountFromQuery<T>(IQuery<T> query)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            return await query.Run(GetDatabaseContext(scope)).CountAsync();
        }
    }

    protected async Task OrganizationUpdateStorage(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var attachments = await dbContext.Ciphers
                .Where(e => e.UserId == null &&
                    e.OrganizationId == organizationId &&
                    !string.IsNullOrWhiteSpace(e.Attachments))
                .Select(e => e.Attachments)
                .ToListAsync();
            var storage = attachments.Sum(e => JsonDocument.Parse(e)?.RootElement.EnumerateObject().Sum(p =>
            {
                if (long.TryParse(p.Value.GetProperty("Size").ToString(), out var s))
                {
                    return s;
                }
                return 0;
            }) ?? 0);
            var organization = new Organization
            {
                Id = organizationId,
                RevisionDate = DateTime.UtcNow,
                Storage = storage,
            };
            dbContext.Organizations.Attach(organization);
            var entry = dbContext.Entry(organization);
            entry.Property(e => e.RevisionDate).IsModified = true;
            entry.Property(e => e.Storage).IsModified = true;
            await dbContext.SaveChangesAsync();
        }
    }

    protected async Task UserUpdateStorage(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var attachments = await dbContext.Ciphers
                .Where(e => e.UserId.HasValue &&
                    e.UserId.Value == userId &&
                    e.OrganizationId == null &&
                    !string.IsNullOrWhiteSpace(e.Attachments))
                .Select(e => e.Attachments)
                .ToListAsync();
            var storage = attachments.Sum(e => JsonDocument.Parse(e)?.RootElement.EnumerateObject().Sum(p =>
            {
                if (long.TryParse(p.Value.GetProperty("Size").ToString(), out var s))
                {
                    return s;
                }
                return 0;
            }) ?? 0);
            var user = new Models.User
            {
                Id = userId,
                RevisionDate = DateTime.UtcNow,
                Storage = storage,
            };
            dbContext.Users.Attach(user);
            var entry = dbContext.Entry(user);
            entry.Property(e => e.RevisionDate).IsModified = true;
            entry.Property(e => e.Storage).IsModified = true;
            await dbContext.SaveChangesAsync();
        }
    }

    protected async Task UserUpdateKeys(User user)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.Users.FindAsync(user.Id);
            if (entity == null)
            {
                return;
            }
            entity.SecurityStamp = user.SecurityStamp;
            entity.Key = user.Key;
            entity.PrivateKey = user.PrivateKey;
            entity.LastKeyRotationDate = user.LastKeyRotationDate;
            entity.AccountRevisionDate = user.AccountRevisionDate;
            entity.RevisionDate = user.RevisionDate;
            await dbContext.SaveChangesAsync();
        }
    }
}
