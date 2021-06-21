using AutoMapper;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;
using EfModel = Bit.Core.Models.EntityFramework;
using LinqToDB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;

namespace Bit.Core.Repositories.EntityFramework
{
    public abstract class BaseEntityFrameworkRepository
    {
        protected BulkCopyOptions DefaultBulkCopyOptions { get; set; } = new BulkCopyOptions
        {
            KeepIdentity = true,
            BulkCopyType = BulkCopyType.ProviderSpecific,
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

        protected async Task UserBumpAccountRevisionDateByCipherId(Cipher cipher)
        {
            var list = new List<Cipher> { cipher };
            await UserBumpAccountRevisionDateByCipherId(list);
        }

        protected async Task UserBumpAccountRevisionDateByCipherId(IEnumerable<Cipher> ciphers)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                foreach (var cipher in ciphers)
                {
                    var dbContext = GetDatabaseContext(scope);
                    var query = new UserBumpAccountRevisionDateByCipherIdQuery(cipher);
                    var users = query.Run(dbContext);

                    await users.ForEachAsync(e => 
                    {
                        dbContext.Attach(e);
                        e.RevisionDate = DateTime.UtcNow;
                    });
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        protected async Task UserBumpAccountRevisionDateByOrganizationId(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new UserBumpAccountRevisionDateByOrganizationIdQuery(organizationId);
                var users = query.Run(dbContext);
                await users.ForEachAsync(e => 
                {
                    dbContext.Attach(e);
                    e.RevisionDate = DateTime.UtcNow;
                });
                await dbContext.SaveChangesAsync();
            }
        }

        protected async Task UserBumpAccountRevisionDate(Guid userId)
        {
            await UserBumpManyAccountRevisionDates(new[] { userId });
        }

        protected async Task UserBumpManyAccountRevisionDates(ICollection<Guid> userIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var users = dbContext.Users.Where(u => userIds.Contains(u.Id));
                await users.ForEachAsync(u => 
                {
                    dbContext.Attach(u);
                    u.RevisionDate = DateTime.UtcNow;
                });
                await dbContext.SaveChangesAsync();
            }
        }

        protected async Task OrganizationUpdateStorage(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ciphers = await dbContext.Ciphers
                    .Where(e => e.UserId == null && e.OrganizationId == organizationId)
                    .ToListAsync();
                var storage = ciphers.Sum(e => 
                {
                    if (string.IsNullOrWhiteSpace(e.Attachments))
                    {
                        return 0;
                    }

                    return JsonDocument.Parse(e.Attachments)?.RootElement.EnumerateArray()
                        .Sum(p => p.GetProperty("Size").GetInt64()) ?? 0;
                });
                var organization = new EfModel.Organization
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
                var ciphers = await dbContext.Ciphers
                    .Where(e => e.UserId.HasValue && e.UserId.Value == userId && e.OrganizationId == null)
                    .ToListAsync();
                var storage = ciphers.Sum(e => 
                {
                    if (string.IsNullOrWhiteSpace(e.Attachments))
                    {
                        return 0;
                    }

                    return JsonDocument.Parse(e.Attachments)?.RootElement.EnumerateArray()
                        .Sum(p => p.GetProperty("Size").GetInt64()) ?? 0;
                });
                var user = new EfModel.User
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
                entity.SecurityStamp = user.SecurityStamp;
                entity.Key = user.Key;
                entity.PrivateKey = user.PrivateKey;
                entity.RevisionDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }

        protected async Task UserBumpAccountRevisionDateByCollectionId(Guid collectionId, Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from u in dbContext.Users
                    join ou in dbContext.OrganizationUsers
                        on u.Id equals ou.UserId
                    join cu in dbContext.CollectionUsers
                        on ou.Id equals cu.OrganizationUserId into cu_g
                    from cu in cu_g.DefaultIfEmpty()
                    where !ou.AccessAll && cu.CollectionId.Equals(collectionId)
                    join gu in dbContext.GroupUsers
                        on ou.Id equals gu.OrganizationUserId into gu_g
                    from gu in gu_g.DefaultIfEmpty()
                    where cu.CollectionId == default(Guid) && !ou.AccessAll
                    join g in dbContext.Groups
                        on gu.GroupId equals g.Id into g_g
                    from g in g_g.DefaultIfEmpty()
                    join cg in dbContext.CollectionGroups
                        on gu.GroupId equals cg.GroupId into cg_g
                    from cg in cg_g.DefaultIfEmpty()
                    where !g.AccessAll && cg.CollectionId == collectionId &&
                        (ou.OrganizationId == organizationId && ou.Status == OrganizationUserStatusType.Confirmed &&
                        (cu.CollectionId != default(Guid) || cg.CollectionId != default(Guid) || ou.AccessAll || g.AccessAll))
                    select new { u, ou, cu, gu, g, cg };
                var users = query.Select(x => x.u);
                await users.ForEachAsync(u => 
                {
                    dbContext.Attach(u);
                    u.RevisionDate = DateTime.UtcNow; 
                });
                await dbContext.SaveChangesAsync();
            }
        }

        protected async Task UserBumpAccountRevisionDateByOrganizationUserId(Guid organizationUserId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from u in dbContext.Users
                    join ou in dbContext.OrganizationUsers
                        on u.Id equals ou.UserId
                    where ou.Id.Equals(organizationUserId) && ou.Status.Equals(OrganizationUserStatusType.Confirmed)
                    select new { u, ou };
                var users = query.Select(x => x.u);
                await users.ForEachAsync(u => 
                {
                    dbContext.Attach(u);
                    u.AccountRevisionDate = DateTime.UtcNow;
                });
                await dbContext.SaveChangesAsync();
            }
        }

        protected async Task UserBumpAccountRevisionDateByProviderUserIds(ICollection<Guid> providerUserIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from pu in dbContext.ProviderUsers
                    join u in dbContext.Users
                        on pu.UserId equals u.Id
                    where pu.Status.Equals(ProviderUserStatusType.Confirmed) &&
                        providerUserIds.Contains(pu.Id)
                    select new { pu, u };
                var users = query.Select(x => x.u);
                await users.ForEachAsync(u => {
                    dbContext.Attach(u);
                    u.AccountRevisionDate = DateTime.UtcNow;
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
