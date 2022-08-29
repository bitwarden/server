using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CollectionCipher = Bit.Core.Entities.CollectionCipher;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class CollectionCipherRepository : BaseEntityFrameworkRepository, ICollectionCipherRepository
{
    public CollectionCipherRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task<CollectionCipher> CreateAsync(CollectionCipher obj)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = Mapper.Map<Models.CollectionCipher>(obj);
            dbContext.Add(entity);
            await dbContext.SaveChangesAsync();
            var organizationId = (await dbContext.Ciphers.FirstOrDefaultAsync(c => c.Id.Equals(obj.CipherId))).OrganizationId;
            if (organizationId.HasValue)
            {
                await UserBumpAccountRevisionDateByCollectionId(obj.CollectionId, organizationId.Value);
            }
            return obj;
        }
    }

    public async Task<ICollection<CollectionCipher>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var data = await (from cc in dbContext.CollectionCiphers
                              join c in dbContext.Collections
                                  on cc.CollectionId equals c.Id
                              where c.OrganizationId == organizationId
                              select cc).ToArrayAsync();
            return data;
        }
    }

    public async Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var data = await new CollectionCipherReadByUserIdQuery(userId)
                .Run(dbContext)
                .ToArrayAsync();
            return data;
        }
    }

    public async Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var data = await new CollectionCipherReadByUserIdCipherIdQuery(userId, cipherId)
                .Run(dbContext)
                .ToArrayAsync();
            return data;
        }
    }

    public async Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizationId = (await dbContext.Ciphers.FindAsync(cipherId)).OrganizationId;
            var availableCollectionsCte = from c in dbContext.Collections
                                          join o in dbContext.Organizations
                                              on c.OrganizationId equals o.Id
                                          join ou in dbContext.OrganizationUsers
                                              on o.Id equals ou.OrganizationId
                                          where ou.UserId == userId
                                          join cu in dbContext.CollectionUsers
                                              on ou.Id equals cu.OrganizationUserId into cu_g
                                          from cu in cu_g.DefaultIfEmpty()
                                          where !ou.AccessAll && cu.CollectionId == c.Id
                                          join gu in dbContext.GroupUsers
                                              on ou.Id equals gu.OrganizationUserId into gu_g
                                          from gu in gu_g.DefaultIfEmpty()
                                          where cu.CollectionId == null && !ou.AccessAll
                                          join g in dbContext.Groups
                                              on gu.GroupId equals g.Id into g_g
                                          from g in g_g.DefaultIfEmpty()
                                          join cg in dbContext.CollectionGroups
                                              on gu.GroupId equals cg.GroupId into cg_g
                                          from cg in cg_g.DefaultIfEmpty()
                                          where !g.AccessAll && cg.CollectionId == c.Id &&
                                          (o.Id == organizationId && o.Enabled && ou.Status == OrganizationUserStatusType.Confirmed && (
                                          ou.AccessAll || !cu.ReadOnly || g.AccessAll || !cg.ReadOnly))
                                          select new { c, o, cu, gu, g, cg };
            var target = from cc in dbContext.CollectionCiphers
                         where cc.CipherId == cipherId
                         select new { cc.CollectionId, cc.CipherId };
            var source = collectionIds.Select(x => new { CollectionId = x, CipherId = cipherId });
            var merge1 = from t in target
                         join s in source
                             on t.CollectionId equals s.CollectionId into s_g
                         from s in s_g.DefaultIfEmpty()
                         where t.CipherId == s.CipherId
                         select new { t, s };
            var merge2 = from s in source
                         join t in target
                             on s.CollectionId equals t.CollectionId into t_g
                         from t in t_g.DefaultIfEmpty()
                         where t.CipherId == s.CipherId
                         select new { t, s };
            var union = merge1.Union(merge2).Distinct();
            var insert = union
                .Where(x => x.t == null && collectionIds.Contains(x.s.CollectionId))
                .Select(x => new Models.CollectionCipher
                {
                    CollectionId = x.s.CollectionId,
                    CipherId = x.s.CipherId,
                });
            var delete = union
                .Where(x => x.s == null && x.t.CipherId == cipherId && collectionIds.Contains(x.t.CollectionId))
                .Select(x => new Models.CollectionCipher
                {
                    CollectionId = x.t.CollectionId,
                    CipherId = x.t.CipherId,
                });
            await dbContext.AddRangeAsync(insert);
            dbContext.RemoveRange(delete);
            await dbContext.SaveChangesAsync();

            if (organizationId.HasValue)
            {
                await UserBumpAccountRevisionDateByOrganizationId(organizationId.Value);
            }
        }
    }

    public async Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var availableCollectionsCte = from c in dbContext.Collections
                                          where c.OrganizationId == organizationId
                                          select c;
            var target = from cc in dbContext.CollectionCiphers
                         where cc.CipherId == cipherId
                         select new { cc.CollectionId, cc.CipherId };
            var source = collectionIds.Select(x => new { CollectionId = x, CipherId = cipherId });
            var merge1 = from t in target
                         join s in source
                             on t.CollectionId equals s.CollectionId into s_g
                         from s in s_g.DefaultIfEmpty()
                         where t.CipherId == s.CipherId
                         select new { t, s };
            var merge2 = from s in source
                         join t in target
                             on s.CollectionId equals t.CollectionId into t_g
                         from t in t_g.DefaultIfEmpty()
                         where t.CipherId == s.CipherId
                         select new { t, s };
            var union = merge1.Union(merge2).Distinct();
            var insert = union
                .Where(x => x.t == null && collectionIds.Contains(x.s.CollectionId))
                .Select(x => new Models.CollectionCipher
                {
                    CollectionId = x.s.CollectionId,
                    CipherId = x.s.CipherId,
                });
            var delete = union
                .Where(x => x.s == null && x.t.CipherId == cipherId)
                .Select(x => new Models.CollectionCipher
                {
                    CollectionId = x.t.CollectionId,
                    CipherId = x.t.CipherId,
                });
            await dbContext.AddRangeAsync(insert);
            dbContext.RemoveRange(delete);
            await dbContext.SaveChangesAsync();
            await UserBumpAccountRevisionDateByOrganizationId(organizationId);
        }
    }

    public async Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId, Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var availibleCollections = from c in dbContext.Collections
                                       join o in dbContext.Organizations
                                           on c.OrganizationId equals o.Id
                                       join ou in dbContext.OrganizationUsers
                                           on o.Id equals ou.OrganizationId
                                       where ou.UserId == userId
                                       join cu in dbContext.CollectionUsers
                                           on ou.Id equals cu.OrganizationUserId into cu_g
                                       from cu in cu_g.DefaultIfEmpty()
                                       where !ou.AccessAll && cu.CollectionId == c.Id
                                       join gu in dbContext.GroupUsers
                                           on ou.Id equals gu.OrganizationUserId into gu_g
                                       from gu in gu_g.DefaultIfEmpty()
                                       where cu.CollectionId == null && !ou.AccessAll
                                       join g in dbContext.Groups
                                           on gu.GroupId equals g.Id into g_g
                                       from g in g_g.DefaultIfEmpty()
                                       join cg in dbContext.CollectionGroups
                                           on gu.GroupId equals cg.GroupId into cg_g
                                       from cg in cg_g.DefaultIfEmpty()
                                       where !g.AccessAll && cg.CollectionId == c.Id &&
                                       (o.Id == organizationId && o.Enabled && ou.Status == OrganizationUserStatusType.Confirmed &&
                                       (ou.AccessAll || !cu.ReadOnly || g.AccessAll || !cg.ReadOnly))
                                       select new { c, o, ou, cu, gu, g, cg };
            var count = await availibleCollections.CountAsync();
            if (await availibleCollections.CountAsync() < 1)
            {
                return;
            }

            var insertData = from collectionId in collectionIds
                             from cipherId in cipherIds
                             where availibleCollections.Select(x => x.c.Id).Contains(collectionId)
                             select new Models.CollectionCipher
                             {
                                 CollectionId = collectionId,
                                 CipherId = cipherId,
                             };
            await dbContext.AddRangeAsync(insertData);
            await UserBumpAccountRevisionDateByOrganizationId(organizationId);
        }
    }
}
