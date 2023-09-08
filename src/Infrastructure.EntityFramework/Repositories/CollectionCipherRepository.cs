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
                await dbContext.UserBumpAccountRevisionDateByCollectionIdAsync(obj.CollectionId, organizationId.Value);
                await dbContext.SaveChangesAsync();
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

            var organizationId = await dbContext.Ciphers
                .Where(c => c.Id == cipherId)
                .Select(c => c.OrganizationId)
                .FirstAsync();

            var availableCollections = await (
                from c in dbContext.Collections
                join o in dbContext.Organizations on c.OrganizationId equals o.Id
                join ou in dbContext.OrganizationUsers
                   on new { OrganizationId = o.Id, UserId = (Guid?)userId } equals
                   new { ou.OrganizationId, ou.UserId }
                join cu in dbContext.CollectionUsers
                   on new { ou.AccessAll, CollectionId = c.Id, OrganizationUserId = ou.Id } equals
                   new { AccessAll = false, cu.CollectionId, cu.OrganizationUserId } into cu_g
                from cu in cu_g.DefaultIfEmpty()
                join gu in dbContext.GroupUsers
                   on new { CollectionId = (Guid?)cu.CollectionId, ou.AccessAll, OrganizationUserId = ou.Id } equals
                   new { CollectionId = (Guid?)null, AccessAll = false, gu.OrganizationUserId } into gu_g
                from gu in gu_g.DefaultIfEmpty()
                join g in dbContext.Groups on gu.GroupId equals g.Id into g_g
                from g in g_g.DefaultIfEmpty()
                join cg in dbContext.CollectionGroups
                   on new { g.AccessAll, CollectionId = c.Id, gu.GroupId } equals
                   new { AccessAll = false, cg.CollectionId, cg.GroupId } into cg_g
                from cg in cg_g.DefaultIfEmpty()
                where o.Id == organizationId && o.Enabled && ou.Status == OrganizationUserStatusType.Confirmed
                   && (ou.AccessAll || !cu.ReadOnly || g.AccessAll || !cg.ReadOnly)
                select c.Id).ToListAsync();

            var collectionCiphers = await (
                from cc in dbContext.CollectionCiphers
                where cc.CipherId == cipherId
                select cc).ToListAsync();

            foreach (var requestedCollectionId in collectionIds)
            {
                // I don't totally agree with t.CipherId = cipherId here because that should have been guaranteed by
                // the WHERE above but the SQL Server CTE has it
                var existingCollectionCipher = collectionCiphers
                    .FirstOrDefault(t => t.CollectionId == requestedCollectionId && t.CipherId == cipherId);
                // requestedCollectionId = SOURCE
                // existingCollectionCipher = TARGET

                // They have to want it selected and it has to exist
                if (existingCollectionCipher == null && availableCollections.Contains(requestedCollectionId))
                {
                    // WHEN NOT MATCHED BY TARGET AND ...
                    dbContext.CollectionCiphers.Add(new Models.CollectionCipher
                    {
                        CollectionId = requestedCollectionId,
                        CipherId = cipherId,
                    });
                }

                // If it has fallen to here it's requested but not actually available to don't add anything
            }

            // Now we need to remove collection ciphers that are no longer requested
            dbContext.CollectionCiphers.RemoveRange(collectionCiphers.Where(cc => !collectionIds.Contains(cc.CollectionId) && cc.CipherId == cipherId));

            if (organizationId.HasValue)
            {
                await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId.Value);
            }
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var availableCollections = await (from c in dbContext.Collections
                                              where c.OrganizationId == organizationId
                                              select c).ToListAsync();

            var currentCollectionCiphers = await (from cc in dbContext.CollectionCiphers
                                                  where cc.CipherId == cipherId
                                                  select cc).ToListAsync();

            foreach (var requestedCollectionId in collectionIds)
            {
                var requestedCollectionCipher = currentCollectionCiphers
                    .FirstOrDefault(cc => cc.CollectionId == requestedCollectionId);

                if (requestedCollectionCipher == null)
                {
                    dbContext.CollectionCiphers.Add(new Models.CollectionCipher
                    {
                        CipherId = cipherId,
                        CollectionId = requestedCollectionId,
                    });
                }
            }

            dbContext.RemoveRange(currentCollectionCiphers.Where(cc => !collectionIds.Contains(cc.CollectionId)));
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId, Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var availableCollections = (
                from c in dbContext.Collections
                join o in dbContext.Organizations
                    on c.OrganizationId equals o.Id
                join ou in dbContext.OrganizationUsers
                    on new { OrganizationId = o.Id, UserId = (Guid?)userId }
                        equals new { ou.OrganizationId, ou.UserId }
                join cu in dbContext.CollectionUsers
                    on new { ou.AccessAll, CollectionId = c.Id }
                        equals new { AccessAll = false, cu.CollectionId } into cu_g
                from cu in cu_g.DefaultIfEmpty()
                join gu in dbContext.GroupUsers
                    on new { CollectionId = (Guid?)cu.CollectionId, ou.AccessAll, OrganizationUserId = ou.Id }
                        equals new { CollectionId = (Guid?)null, AccessAll = false, gu.OrganizationUserId } into gu_g
                from gu in gu_g.DefaultIfEmpty()
                join g in dbContext.Groups
                    on gu.GroupId equals g.Id into g_g
                from g in g_g.DefaultIfEmpty()
                join cg in dbContext.CollectionGroups
                    on new { g.AccessAll, CollectionId = c.Id, gu.GroupId }
                        equals new { AccessAll = false, cg.CollectionId, cg.GroupId } into cg_g
                from cg in cg_g.DefaultIfEmpty()
                where o.Id == organizationId
                    && o.Enabled
                    && ou.Status == OrganizationUserStatusType.Confirmed
                    && (ou.AccessAll || !cu.ReadOnly || g.AccessAll || !cg.ReadOnly)
                select c.Id
            );

            if (!await availableCollections.AnyAsync())
            {
                return;
            }

            var insertData = (
                from collectionId in collectionIds
                from cipherId in cipherIds
                where availableCollections.Contains(collectionId)
                select new Models.CollectionCipher
                {
                    CollectionId = collectionId,
                    CipherId = cipherId,
                });
            await dbContext.AddRangeAsync(insertData);
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId);
            await dbContext.SaveChangesAsync();
        }
    }
}
