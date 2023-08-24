using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class CollectionRepository : Repository<Core.Entities.Collection, Collection, Guid>, ICollectionRepository
{
    public CollectionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Collections)
    { }

    public override async Task<Core.Entities.Collection> CreateAsync(Core.Entities.Collection collection)
    {
        await base.CreateAsync(collection);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByCollectionIdAsync(collection.Id, collection.OrganizationId);
            await dbContext.SaveChangesAsync();
        }
        return collection;
    }

    public override async Task DeleteAsync(Core.Entities.Collection collection)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByCollectionIdAsync(collection.Id, collection.OrganizationId);
            await dbContext.SaveChangesAsync();
        }
        await base.DeleteAsync(collection);
    }

    public override async Task UpsertAsync(Core.Entities.Collection collection)
    {
        await base.UpsertAsync(collection);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByCollectionIdAsync(collection.Id, collection.OrganizationId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task CreateAsync(Core.Entities.Collection obj, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users)
    {
        await CreateAsync(obj);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            if (groups != null)
            {
                var availableGroups = await (from g in dbContext.Groups
                                             where g.OrganizationId == obj.OrganizationId
                                             select g.Id).ToListAsync();
                var collectionGroups = groups
                    .Where(g => availableGroups.Contains(g.Id))
                    .Select(g => new CollectionGroup
                    {
                        CollectionId = obj.Id,
                        GroupId = g.Id,
                        ReadOnly = g.ReadOnly,
                        HidePasswords = g.HidePasswords,
                        Manage = g.Manage
                    });
                await dbContext.AddRangeAsync(collectionGroups);
            }

            if (users != null)
            {
                var availableUsers = await (from g in dbContext.OrganizationUsers
                                            where g.OrganizationId == obj.OrganizationId
                                            select g.Id).ToListAsync();
                var collectionUsers = users
                    .Where(u => availableUsers.Contains(u.Id))
                    .Select(u => new CollectionUser
                    {
                        CollectionId = obj.Id,
                        OrganizationUserId = u.Id,
                        ReadOnly = u.ReadOnly,
                        HidePasswords = u.HidePasswords,
                        Manage = u.Manage
                    });
                await dbContext.AddRangeAsync(collectionUsers);
            }
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(obj.OrganizationId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteUserAsync(Guid collectionId, Guid organizationUserId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from cu in dbContext.CollectionUsers
                        where cu.CollectionId == collectionId &&
                            cu.OrganizationUserId == organizationUserId
                        select cu;
            dbContext.RemoveRange(await query.ToListAsync());
            await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdAsync(organizationUserId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return (await GetManyByUserIdAsync(userId)).FirstOrDefault(c => c.Id == id);
        }
    }

    public async Task<Tuple<Core.Entities.Collection, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id)
    {
        var collection = await base.GetByIdAsync(id);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var groupQuery = from cg in dbContext.CollectionGroups
                             where cg.CollectionId.Equals(id)
                             select new CollectionAccessSelection
                             {
                                 Id = cg.GroupId,
                                 ReadOnly = cg.ReadOnly,
                                 HidePasswords = cg.HidePasswords,
                                 Manage = cg.Manage
                             };
            var groups = await groupQuery.ToArrayAsync();

            var userQuery = from cg in dbContext.CollectionUsers
                            where cg.CollectionId.Equals(id)
                            select new CollectionAccessSelection
                            {
                                Id = cg.OrganizationUserId,
                                ReadOnly = cg.ReadOnly,
                                HidePasswords = cg.HidePasswords,
                                Manage = cg.Manage
                            };
            var users = await userQuery.ToArrayAsync();
            var access = new CollectionAccessDetails { Users = users, Groups = groups };

            return new Tuple<Core.Entities.Collection, CollectionAccessDetails>(collection, access);
        }
    }

    public async Task<Tuple<CollectionDetails, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id, Guid userId)
    {
        var collection = await GetByIdAsync(id, userId);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var groupQuery = from cg in dbContext.CollectionGroups
                             where cg.CollectionId.Equals(id)
                             select new CollectionAccessSelection
                             {
                                 Id = cg.GroupId,
                                 ReadOnly = cg.ReadOnly,
                                 HidePasswords = cg.HidePasswords,
                                 Manage = cg.Manage
                             };
            var groups = await groupQuery.ToArrayAsync();

            var userQuery = from cg in dbContext.CollectionUsers
                            where cg.CollectionId.Equals(id)
                            select new CollectionAccessSelection
                            {
                                Id = cg.OrganizationUserId,
                                ReadOnly = cg.ReadOnly,
                                HidePasswords = cg.HidePasswords,
                                Manage = cg.Manage,
                            };
            var users = await userQuery.ToArrayAsync();
            var access = new CollectionAccessDetails { Users = users, Groups = groups };

            return new Tuple<CollectionDetails, CollectionAccessDetails>(collection, access);
        }
    }

    public async Task<ICollection<Tuple<Core.Entities.Collection, CollectionAccessDetails>>> GetManyByOrganizationIdWithAccessAsync(Guid organizationId)
    {
        var collections = await GetManyByOrganizationIdAsync(organizationId);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var groups =
                from c in collections
                join cg in dbContext.CollectionGroups on c.Id equals cg.CollectionId
                group cg by cg.CollectionId into g
                select g;
            var users =
                from c in collections
                join cu in dbContext.CollectionUsers on c.Id equals cu.CollectionId
                group cu by cu.CollectionId into u
                select u;

            return collections.Select(collection =>
                new Tuple<Core.Entities.Collection, CollectionAccessDetails>(
                    collection,
                    new CollectionAccessDetails
                    {
                        Groups = groups
                            .FirstOrDefault(g => g.Key == collection.Id)?
                            .Select(g => new CollectionAccessSelection
                            {
                                Id = g.GroupId,
                                HidePasswords = g.HidePasswords,
                                ReadOnly = g.ReadOnly,
                                Manage = g.Manage
                            }).ToList() ?? new List<CollectionAccessSelection>(),
                        Users = users
                            .FirstOrDefault(u => u.Key == collection.Id)?
                            .Select(c => new CollectionAccessSelection
                            {
                                Id = c.OrganizationUserId,
                                HidePasswords = c.HidePasswords,
                                ReadOnly = c.ReadOnly,
                                Manage = c.Manage
                            }).ToList() ?? new List<CollectionAccessSelection>()
                    }
                )
            ).ToList();
        }
    }

    public async Task<ICollection<Tuple<Core.Entities.Collection, CollectionAccessDetails>>> GetManyByUserIdWithAccessAsync(Guid userId, Guid organizationId)
    {
        var collections = (await GetManyByUserIdAsync(userId)).Where(c => c.OrganizationId == organizationId).ToList();
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var groups =
                from c in collections
                join cg in dbContext.CollectionGroups on c.Id equals cg.CollectionId
                group cg by cg.CollectionId into g
                select g;
            var users =
                from c in collections
                join cu in dbContext.CollectionUsers on c.Id equals cu.CollectionId
                group cu by cu.CollectionId into u
                select u;

            return collections.Select(collection =>
                new Tuple<Core.Entities.Collection, CollectionAccessDetails>(
                    collection,
                    new CollectionAccessDetails
                    {
                        Groups = groups
                            .FirstOrDefault(g => g.Key == collection.Id)?
                            .Select(g => new CollectionAccessSelection
                            {
                                Id = g.GroupId,
                                HidePasswords = g.HidePasswords,
                                ReadOnly = g.ReadOnly,
                                Manage = g.Manage
                            }).ToList() ?? new List<CollectionAccessSelection>(),
                        Users = users
                            .FirstOrDefault(u => u.Key == collection.Id)?
                            .Select(c => new CollectionAccessSelection
                            {
                                Id = c.OrganizationUserId,
                                HidePasswords = c.HidePasswords,
                                ReadOnly = c.ReadOnly,
                                Manage = c.Manage
                            }).ToList() ?? new List<CollectionAccessSelection>()
                    }
                )
            ).ToList();
        }
    }

    public async Task<ICollection<Core.Entities.Collection>> GetManyByManyIdsAsync(IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from c in dbContext.Collections
                        where collectionIds.Contains(c.Id)
                        select c;
            var data = await query.ToArrayAsync();
            return data;
        }
    }

    public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
    {
        var query = new CollectionReadCountByOrganizationIdQuery(organizationId);
        return await GetCountFromQuery(query);
    }

    public async Task<ICollection<Core.Entities.Collection>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from c in dbContext.Collections
                        where c.OrganizationId == organizationId
                        select c;
            var collections = await query.ToArrayAsync();
            return collections;
        }
    }

    public async Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var baseCollectionQuery = new UserCollectionDetailsQuery(userId).Run(dbContext);

            if (dbContext.Database.IsSqlite())
            {
                return (await baseCollectionQuery.ToListAsync())
                    .GroupBy(c => new
                    {
                        c.Id,
                        c.OrganizationId,
                        c.Name,
                        c.CreationDate,
                        c.RevisionDate,
                        c.ExternalId
                    })
                    .Select(collectionGroup => new CollectionDetails
                    {
                        Id = collectionGroup.Key.Id,
                        OrganizationId = collectionGroup.Key.OrganizationId,
                        Name = collectionGroup.Key.Name,
                        CreationDate = collectionGroup.Key.CreationDate,
                        RevisionDate = collectionGroup.Key.RevisionDate,
                        ExternalId = collectionGroup.Key.ExternalId,
                        ReadOnly = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.ReadOnly))),
                        HidePasswords = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.HidePasswords))),
                        Manage = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.Manage))),
                    })
                    .ToList();
            }

            return await (from c in baseCollectionQuery
                          group c by new
                          {
                              c.Id,
                              c.OrganizationId,
                              c.Name,
                              c.CreationDate,
                              c.RevisionDate,
                              c.ExternalId
                          } into collectionGroup
                          select new CollectionDetails
                          {
                              Id = collectionGroup.Key.Id,
                              OrganizationId = collectionGroup.Key.OrganizationId,
                              Name = collectionGroup.Key.Name,
                              CreationDate = collectionGroup.Key.CreationDate,
                              RevisionDate = collectionGroup.Key.RevisionDate,
                              ExternalId = collectionGroup.Key.ExternalId,
                              ReadOnly = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.ReadOnly))),
                              HidePasswords = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.HidePasswords))),
                              Manage = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.Manage))),
                          }).ToListAsync();
        }
    }

    public async Task<ICollection<CollectionAccessSelection>> GetManyUsersByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from cu in dbContext.CollectionUsers
                        where cu.CollectionId == id
                        select cu;
            var collectionUsers = await query.ToListAsync();
            return collectionUsers.Select(cu => new CollectionAccessSelection
            {
                Id = cu.OrganizationUserId,
                ReadOnly = cu.ReadOnly,
                HidePasswords = cu.HidePasswords,
                Manage = cu.Manage
            }).ToArray();
        }
    }

    public async Task ReplaceAsync(Core.Entities.Collection collection, IEnumerable<CollectionAccessSelection> groups,
        IEnumerable<CollectionAccessSelection> users)
    {
        await UpsertAsync(collection);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await ReplaceCollectionGroupsAsync(dbContext, collection, groups);
            await ReplaceCollectionUsersAsync(dbContext, collection, users);
            await dbContext.UserBumpAccountRevisionDateByCollectionIdAsync(collection.Id, collection.OrganizationId);
        }
    }

    public async Task UpdateUsersAsync(Guid id, IEnumerable<CollectionAccessSelection> requestedUsers)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var organizationId = await dbContext.Collections
                .Where(c => c.Id == id)
                .Select(c => c.OrganizationId)
                .FirstOrDefaultAsync();

            var existingCollectionUsers = await dbContext.CollectionUsers
                .Where(cu => cu.CollectionId == id)
                .ToListAsync();

            foreach (var requestedUser in requestedUsers)
            {
                var existingCollectionUser = existingCollectionUsers.FirstOrDefault(cu => cu.OrganizationUserId == requestedUser.Id);
                if (existingCollectionUser == null)
                {
                    // This is a brand new entry
                    dbContext.CollectionUsers.Add(new CollectionUser
                    {
                        CollectionId = id,
                        OrganizationUserId = requestedUser.Id,
                        HidePasswords = requestedUser.HidePasswords,
                        ReadOnly = requestedUser.ReadOnly,
                        Manage = requestedUser.Manage
                    });
                    continue;
                }

                // It already exists, update it
                existingCollectionUser.HidePasswords = requestedUser.HidePasswords;
                existingCollectionUser.ReadOnly = requestedUser.ReadOnly;
                existingCollectionUser.Manage = requestedUser.Manage;
                dbContext.CollectionUsers.Update(existingCollectionUser);
            }

            // Remove all existing ones that are no longer requested
            var requestedUserIds = requestedUsers.Select(u => u.Id);
            dbContext.CollectionUsers.RemoveRange(existingCollectionUsers.Where(cu => !requestedUserIds.Contains(cu.OrganizationUserId)));
            // Need to save the new collection users before running the bump revision code
            await dbContext.SaveChangesAsync();
            await dbContext.UserBumpAccountRevisionDateByCollectionIdAsync(id, organizationId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var collectionGroupEntities = await dbContext.CollectionGroups
                .Where(cg => collectionIds.Contains(cg.CollectionId))
                .ToListAsync();
            var collectionEntities = await dbContext.Collections
                .Where(c => collectionIds.Contains(c.Id))
                .ToListAsync();

            dbContext.CollectionGroups.RemoveRange(collectionGroupEntities);
            dbContext.Collections.RemoveRange(collectionEntities);
            await dbContext.SaveChangesAsync();

            foreach (var collection in collectionEntities.GroupBy(g => g.Organization.Id))
            {
                await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(collection.Key);
            }
        }
    }

    public async Task CreateOrUpdateAccessForManyAsync(Guid organizationId, IEnumerable<Guid> collectionIds,
        IEnumerable<CollectionAccessSelection> users, IEnumerable<CollectionAccessSelection> groups)
    {
    }

    private async Task ReplaceCollectionGroupsAsync(DatabaseContext dbContext, Core.Entities.Collection collection, IEnumerable<CollectionAccessSelection> groups)
    {
        var groupsInOrg = dbContext.Groups.Where(g => g.OrganizationId == collection.OrganizationId);
        var modifiedGroupEntities = dbContext.Groups.Where(x => groups.Select(x => x.Id).Contains(x.Id));
        var target = (from cg in dbContext.CollectionGroups
                      join g in modifiedGroupEntities
                          on cg.CollectionId equals collection.Id into s_g
                      from g in s_g.DefaultIfEmpty()
                      where g == null || cg.GroupId == g.Id
                      select new { cg, g }).AsNoTracking();
        var source = (from g in modifiedGroupEntities
                      from cg in dbContext.CollectionGroups
                          .Where(cg => cg.CollectionId == collection.Id && cg.GroupId == g.Id).DefaultIfEmpty()
                      select new { cg, g }).AsNoTracking();
        var union = await target
            .Union(source)
            .Where(x =>
                x.cg == null ||
                ((x.g == null || x.g.Id == x.cg.GroupId) &&
                (x.cg.CollectionId == collection.Id)))
            .AsNoTracking()
            .ToListAsync();
        var insert = union.Where(x => x.cg == null && groupsInOrg.Any(c => x.g.Id == c.Id))
            .Select(x => new CollectionGroup
            {
                CollectionId = collection.Id,
                GroupId = x.g.Id,
                ReadOnly = groups.FirstOrDefault(g => g.Id == x.g.Id).ReadOnly,
                HidePasswords = groups.FirstOrDefault(g => g.Id == x.g.Id).HidePasswords,
                Manage = groups.FirstOrDefault(g => g.Id == x.g.Id).Manage
            }).ToList();
        var update = union
            .Where(
                x => x.g != null &&
                x.cg != null &&
                (x.cg.ReadOnly != groups.FirstOrDefault(g => g.Id == x.g.Id).ReadOnly ||
                x.cg.HidePasswords != groups.FirstOrDefault(g => g.Id == x.g.Id).HidePasswords ||
                x.cg.Manage != groups.FirstOrDefault(g => g.Id == x.g.Id).Manage)
            )
            .Select(x => new CollectionGroup
            {
                CollectionId = collection.Id,
                GroupId = x.g.Id,
                ReadOnly = groups.FirstOrDefault(g => g.Id == x.g.Id).ReadOnly,
                HidePasswords = groups.FirstOrDefault(g => g.Id == x.g.Id).HidePasswords,
                Manage = groups.FirstOrDefault(g => g.Id == x.g.Id).Manage,
            });
        var delete = union
            .Where(
                x => x.g == null &&
                x.cg.CollectionId == collection.Id
            )
            .Select(x => new CollectionGroup
            {
                CollectionId = collection.Id,
                GroupId = x.cg.GroupId,
            })
            .ToList();

        await dbContext.AddRangeAsync(insert);
        dbContext.UpdateRange(update);
        dbContext.RemoveRange(delete);
        await dbContext.SaveChangesAsync();
    }

    private async Task ReplaceCollectionUsersAsync(DatabaseContext dbContext, Core.Entities.Collection collection, IEnumerable<CollectionAccessSelection> users)
    {
        var usersInOrg = dbContext.OrganizationUsers.Where(u => u.OrganizationId == collection.OrganizationId);
        var modifiedUserEntities = dbContext.OrganizationUsers.Where(x => users.Select(x => x.Id).Contains(x.Id));
        var target = (from cu in dbContext.CollectionUsers
                      join u in modifiedUserEntities
                          on cu.CollectionId equals collection.Id into s_g
                      from u in s_g.DefaultIfEmpty()
                      where u == null || cu.OrganizationUserId == u.Id
                      select new { cu, u }).AsNoTracking();
        var source = (from u in modifiedUserEntities
                      from cu in dbContext.CollectionUsers
                          .Where(cu => cu.CollectionId == collection.Id && cu.OrganizationUserId == u.Id).DefaultIfEmpty()
                      select new { cu, u }).AsNoTracking();
        var union = await target
            .Union(source)
            .Where(x =>
                x.cu == null ||
                ((x.u == null || x.u.Id == x.cu.OrganizationUserId) &&
                (x.cu.CollectionId == collection.Id)))
            .AsNoTracking()
            .ToListAsync();
        var insert = union.Where(x => x.u == null && usersInOrg.Any(c => x.u.Id == c.Id))
            .Select(x => new CollectionUser
            {
                CollectionId = collection.Id,
                OrganizationUserId = x.u.Id,
                ReadOnly = users.FirstOrDefault(u => u.Id == x.u.Id).ReadOnly,
                HidePasswords = users.FirstOrDefault(u => u.Id == x.u.Id).HidePasswords,
                Manage = users.FirstOrDefault(u => u.Id == x.u.Id).Manage,
            }).ToList();
        var update = union
            .Where(
                x => x.u != null &&
                x.cu != null &&
                (x.cu.ReadOnly != users.FirstOrDefault(u => u.Id == x.u.Id).ReadOnly ||
                x.cu.HidePasswords != users.FirstOrDefault(u => u.Id == x.u.Id).HidePasswords ||
                x.cu.Manage != users.FirstOrDefault(u => u.Id == x.u.Id).Manage)
            )
            .Select(x => new CollectionUser
            {
                CollectionId = collection.Id,
                OrganizationUserId = x.u.Id,
                ReadOnly = users.FirstOrDefault(u => u.Id == x.u.Id).ReadOnly,
                HidePasswords = users.FirstOrDefault(u => u.Id == x.u.Id).HidePasswords,
                Manage = users.FirstOrDefault(u => u.Id == x.u.Id).Manage,
            });
        var delete = union
            .Where(
                x => x.u == null &&
                x.cu.CollectionId == collection.Id
            )
            .Select(x => new CollectionUser
            {
                CollectionId = collection.Id,
                OrganizationUserId = x.cu.OrganizationUserId,
            })
            .ToList();

        await dbContext.AddRangeAsync(insert);
        dbContext.UpdateRange(update);
        dbContext.RemoveRange(delete);
        await dbContext.SaveChangesAsync();
    }
}
