﻿using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

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

    public async Task CreateAsync(Core.Entities.Collection obj, IEnumerable<CollectionAccessSelection>? groups, IEnumerable<CollectionAccessSelection>? users)
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

    public async Task<Tuple<Core.Entities.Collection?, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id)
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

            return new Tuple<Core.Entities.Collection?, CollectionAccessDetails>(collection, access);
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
                        Manage = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Manage))),
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
                              Manage = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Manage))),
                          }).ToListAsync();
        }
    }

    public async Task<ICollection<CollectionAdminDetails>> GetManyByOrganizationIdWithPermissionsAsync(
        Guid organizationId, Guid userId, bool includeAccessRelationships)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = CollectionAdminDetailsQuery.ByOrganizationId(organizationId, userId).Run(dbContext);

            ICollection<CollectionAdminDetails> collections;

            // SQLite does not support the GROUP BY clause
            if (dbContext.Database.IsSqlite())
            {
                collections = (await query.ToListAsync())
                    .GroupBy(c => new
                    {
                        c.Id,
                        c.OrganizationId,
                        c.Name,
                        c.CreationDate,
                        c.RevisionDate,
                        c.ExternalId,
                        c.Unmanaged
                    }).Select(collectionGroup => new CollectionAdminDetails
                    {
                        Id = collectionGroup.Key.Id,
                        OrganizationId = collectionGroup.Key.OrganizationId,
                        Name = collectionGroup.Key.Name,
                        CreationDate = collectionGroup.Key.CreationDate,
                        RevisionDate = collectionGroup.Key.RevisionDate,
                        ExternalId = collectionGroup.Key.ExternalId,
                        ReadOnly = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.ReadOnly))),
                        HidePasswords =
                            Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.HidePasswords))),
                        Manage = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Manage))),
                        Assigned = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Assigned))),
                        Unmanaged = collectionGroup.Key.Unmanaged
                    }).ToList();
            }
            else
            {
                collections = await (from c in query
                                     group c by new
                                     {
                                         c.Id,
                                         c.OrganizationId,
                                         c.Name,
                                         c.CreationDate,
                                         c.RevisionDate,
                                         c.ExternalId,
                                         c.Unmanaged
                                     }
                    into collectionGroup
                                     select new CollectionAdminDetails
                                     {
                                         Id = collectionGroup.Key.Id,
                                         OrganizationId = collectionGroup.Key.OrganizationId,
                                         Name = collectionGroup.Key.Name,
                                         CreationDate = collectionGroup.Key.CreationDate,
                                         RevisionDate = collectionGroup.Key.RevisionDate,
                                         ExternalId = collectionGroup.Key.ExternalId,
                                         ReadOnly = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.ReadOnly))),
                                         HidePasswords =
                                             Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.HidePasswords))),
                                         Manage = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Manage))),
                                         Assigned = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Assigned))),
                                         Unmanaged = collectionGroup.Key.Unmanaged
                                     }).ToListAsync();
            }

            if (!includeAccessRelationships)
            {
                return collections;
            }

            var groups = (from c in collections
                          join cg in dbContext.CollectionGroups on c.Id equals cg.CollectionId
                          group cg by cg.CollectionId into g
                          select g).ToList();

            var users = (from c in collections
                         join cu in dbContext.CollectionUsers on c.Id equals cu.CollectionId
                         group cu by cu.CollectionId into u
                         select u).ToList();

            foreach (var collection in collections)
            {
                collection.Groups = groups
                    .FirstOrDefault(g => g.Key == collection.Id)?
                    .Select(g => new CollectionAccessSelection
                    {
                        Id = g.GroupId,
                        HidePasswords = g.HidePasswords,
                        ReadOnly = g.ReadOnly,
                        Manage = g.Manage,
                    }).ToList() ?? new List<CollectionAccessSelection>();
                collection.Users = users
                    .FirstOrDefault(u => u.Key == collection.Id)?
                    .Select(c => new CollectionAccessSelection
                    {
                        Id = c.OrganizationUserId,
                        HidePasswords = c.HidePasswords,
                        ReadOnly = c.ReadOnly,
                        Manage = c.Manage
                    }).ToList() ?? new List<CollectionAccessSelection>();
            }

            return collections;
        }
    }

    public async Task<CollectionAdminDetails?> GetByIdWithPermissionsAsync(Guid collectionId, Guid? userId,
        bool includeAccessRelationships)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = CollectionAdminDetailsQuery.ByCollectionId(collectionId, userId).Run(dbContext);

            CollectionAdminDetails? collectionDetails;

            // SQLite does not support the GROUP BY clause
            if (dbContext.Database.IsSqlite())
            {
                collectionDetails = (await query.ToListAsync())
                    .GroupBy(c => new
                    {
                        c.Id,
                        c.OrganizationId,
                        c.Name,
                        c.CreationDate,
                        c.RevisionDate,
                        c.ExternalId
                    }).Select(collectionGroup => new CollectionAdminDetails
                    {
                        Id = collectionGroup.Key.Id,
                        OrganizationId = collectionGroup.Key.OrganizationId,
                        Name = collectionGroup.Key.Name,
                        CreationDate = collectionGroup.Key.CreationDate,
                        RevisionDate = collectionGroup.Key.RevisionDate,
                        ExternalId = collectionGroup.Key.ExternalId,
                        ReadOnly = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.ReadOnly))),
                        HidePasswords =
                            Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.HidePasswords))),
                        Manage = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Manage))),
                        Assigned = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Assigned))),
                        Unmanaged = collectionGroup.Select(c => c.Unmanaged).FirstOrDefault()
                    }).FirstOrDefault();
            }
            else
            {
                collectionDetails = await (from c in query
                                           group c by new
                                           {
                                               c.Id,
                                               c.OrganizationId,
                                               c.Name,
                                               c.CreationDate,
                                               c.RevisionDate,
                                               c.ExternalId
                                           }
                    into collectionGroup
                                           select new CollectionAdminDetails
                                           {
                                               Id = collectionGroup.Key.Id,
                                               OrganizationId = collectionGroup.Key.OrganizationId,
                                               Name = collectionGroup.Key.Name,
                                               CreationDate = collectionGroup.Key.CreationDate,
                                               RevisionDate = collectionGroup.Key.RevisionDate,
                                               ExternalId = collectionGroup.Key.ExternalId,
                                               ReadOnly = Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.ReadOnly))),
                                               HidePasswords =
                                                   Convert.ToBoolean(collectionGroup.Min(c => Convert.ToInt32(c.HidePasswords))),
                                               Manage = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Manage))),
                                               Assigned = Convert.ToBoolean(collectionGroup.Max(c => Convert.ToInt32(c.Assigned))),
                                               Unmanaged = collectionGroup.Select(c => c.Unmanaged).FirstOrDefault()
                                           }).FirstOrDefaultAsync();
            }

            if (!includeAccessRelationships)
            {
                return collectionDetails;
            }

            var groupsQuery = from cg in dbContext.CollectionGroups
                              where cg.CollectionId.Equals(collectionId)
                              select new CollectionAccessSelection
                              {
                                  Id = cg.GroupId,
                                  ReadOnly = cg.ReadOnly,
                                  HidePasswords = cg.HidePasswords,
                                  Manage = cg.Manage
                              };
            // TODO-NRE: Probably need to null check and return early
            collectionDetails!.Groups = await groupsQuery.ToListAsync();

            var usersQuery = from cg in dbContext.CollectionUsers
                             where cg.CollectionId.Equals(collectionId)
                             select new CollectionAccessSelection
                             {
                                 Id = cg.OrganizationUserId,
                                 ReadOnly = cg.ReadOnly,
                                 HidePasswords = cg.HidePasswords,
                                 Manage = cg.Manage
                             };
            collectionDetails.Users = await usersQuery.ToListAsync();

            return collectionDetails;
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

    public async Task ReplaceAsync(Core.Entities.Collection collection, IEnumerable<CollectionAccessSelection>? groups,
        IEnumerable<CollectionAccessSelection>? users)
    {
        await UpsertAsync(collection);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            if (groups != null)
            {
                await ReplaceCollectionGroupsAsync(dbContext, collection, groups);
            }
            if (users != null)
            {
                await ReplaceCollectionUsersAsync(dbContext, collection, users);
            }
            await dbContext.UserBumpAccountRevisionDateByCollectionIdAsync(collection.Id, collection.OrganizationId);
            await dbContext.SaveChangesAsync();
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
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var collectionIdsList = collectionIds.ToList();

            if (users != null)
            {
                var existingCollectionUsers = await dbContext.CollectionUsers
                    .Where(cu => collectionIdsList.Contains(cu.CollectionId))
                    .ToDictionaryAsync(x => (x.CollectionId, x.OrganizationUserId));

                var requestedUsers = users.ToList();

                foreach (var collectionId in collectionIdsList)
                {
                    foreach (var requestedUser in requestedUsers)
                    {
                        if (!existingCollectionUsers.TryGetValue(
                                (collectionId, requestedUser.Id),
                                out var existingCollectionUser)
                            )
                        {
                            // This is a brand new entry
                            dbContext.CollectionUsers.Add(new CollectionUser
                            {
                                CollectionId = collectionId,
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
                }
            }

            if (groups != null)
            {
                var existingCollectionGroups = await dbContext.CollectionGroups
                    .Where(cu => collectionIdsList.Contains(cu.CollectionId))
                    .ToDictionaryAsync(x => (x.CollectionId, x.GroupId));

                var requestedGroups = groups.ToList();

                foreach (var collectionId in collectionIdsList)
                {
                    foreach (var requestedGroup in requestedGroups)
                    {
                        if (!existingCollectionGroups.TryGetValue(
                                (collectionId, requestedGroup.Id),
                                out var existingCollectionGroup)
                           )
                        {
                            // This is a brand new entry
                            dbContext.CollectionGroups.Add(new CollectionGroup()
                            {
                                CollectionId = collectionId,
                                GroupId = requestedGroup.Id,
                                HidePasswords = requestedGroup.HidePasswords,
                                ReadOnly = requestedGroup.ReadOnly,
                                Manage = requestedGroup.Manage
                            });
                            continue;
                        }

                        // It already exists, update it
                        existingCollectionGroup.HidePasswords = requestedGroup.HidePasswords;
                        existingCollectionGroup.ReadOnly = requestedGroup.ReadOnly;
                        existingCollectionGroup.Manage = requestedGroup.Manage;
                        dbContext.CollectionGroups.Update(existingCollectionGroup);
                    }
                }
            }
            // Need to save the new collection users/groups before running the bump revision code
            await dbContext.SaveChangesAsync();
            await dbContext.UserBumpAccountRevisionDateByCollectionIdsAsync(collectionIdsList, organizationId);
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task ReplaceCollectionGroupsAsync(DatabaseContext dbContext, Core.Entities.Collection collection, IEnumerable<CollectionAccessSelection> groups)
    {
        var existingCollectionGroups = await dbContext.CollectionGroups
            .Where(cg => cg.CollectionId == collection.Id)
            .ToDictionaryAsync(cg => cg.GroupId);

        foreach (var group in groups)
        {
            if (existingCollectionGroups.TryGetValue(group.Id, out var existingCollectionGroup))
            {
                // It already exists, update it
                existingCollectionGroup.HidePasswords = group.HidePasswords;
                existingCollectionGroup.ReadOnly = group.ReadOnly;
                existingCollectionGroup.Manage = group.Manage;
                dbContext.CollectionGroups.Update(existingCollectionGroup);
            }
            else
            {
                // This is a brand new entry, add it
                dbContext.CollectionGroups.Add(new CollectionGroup
                {
                    GroupId = group.Id,
                    CollectionId = collection.Id,
                    HidePasswords = group.HidePasswords,
                    ReadOnly = group.ReadOnly,
                    Manage = group.Manage,
                });
            }
        }

        var requestedGroupIds = groups.Select(g => g.Id).ToArray();
        var toDelete = existingCollectionGroups.Values.Where(cg => !requestedGroupIds.Contains(cg.GroupId));
        dbContext.CollectionGroups.RemoveRange(toDelete);
        // SaveChangesAsync is expected to be called outside this method
    }

    private static async Task ReplaceCollectionUsersAsync(DatabaseContext dbContext, Core.Entities.Collection collection, IEnumerable<CollectionAccessSelection> users)
    {
        var existingCollectionUsers = await dbContext.CollectionUsers
            .Where(cu => cu.CollectionId == collection.Id)
            .ToDictionaryAsync(cu => cu.OrganizationUserId);

        foreach (var user in users)
        {
            if (existingCollectionUsers.TryGetValue(user.Id, out var existingCollectionUser))
            {
                // This is an existing entry, update it.
                existingCollectionUser.HidePasswords = user.HidePasswords;
                existingCollectionUser.ReadOnly = user.ReadOnly;
                existingCollectionUser.Manage = user.Manage;
                dbContext.CollectionUsers.Update(existingCollectionUser);
            }
            else
            {
                // This is a brand new entry, add it
                dbContext.CollectionUsers.Add(new CollectionUser
                {
                    OrganizationUserId = user.Id,
                    CollectionId = collection.Id,
                    HidePasswords = user.HidePasswords,
                    ReadOnly = user.ReadOnly,
                    Manage = user.Manage,
                });
            }
        }

        var requestedUserIds = users.Select(u => u.Id).ToArray();
        var toDelete = existingCollectionUsers.Values.Where(cu => !requestedUserIds.Contains(cu.OrganizationUserId));
        dbContext.CollectionUsers.RemoveRange(toDelete);
        // SaveChangesAsync is expected to be called outside this method
    }
}
