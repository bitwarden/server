using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Microsoft.EntityFrameworkCore;
using EfModel = Bit.Core.Models.EntityFramework;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CollectionUserUpdateUsersQuery
    {
        public readonly CollectionUserUpdateUsersInsertQuery Insert;
        public readonly CollectionUserUpdateUsersUpdateQuery Update;
        public readonly CollectionUserUpdateUsersDeleteQuery Delete;

        public CollectionUserUpdateUsersQuery(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            Insert = new CollectionUserUpdateUsersInsertQuery(collectionId, users);
            Update = new CollectionUserUpdateUsersUpdateQuery(collectionId, users);
            Delete = new CollectionUserUpdateUsersDeleteQuery(collectionId, users);
        }
    }

    public class CollectionUserUpdateUsersInsertQuery : IQuery<EfModel.OrganizationUser>
    {
        private readonly Guid _collectionId;
        private readonly IEnumerable<SelectionReadOnly> _users;

        public CollectionUserUpdateUsersInsertQuery(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            _collectionId = collectionId;
            _users = users;
        }

        public IQueryable<EfModel.OrganizationUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == _collectionId)?.OrganizationId;
            var organizationUserIds = _users.Select(u => u.Id);
            var insertQuery = from ou in dbContext.OrganizationUsers
                              where
                                  organizationUserIds.Contains(ou.Id) &&
                                  ou.OrganizationId == orgId &&
                                  !dbContext.CollectionUsers.Any(
                                      x => x.CollectionId != _collectionId && x.OrganizationUserId == ou.Id)
                              select ou;
            return insertQuery;
        }

        public async Task<IEnumerable<EfModel.CollectionUser>> BuildInMemory(DatabaseContext dbContext)
        {
            var data = await Run(dbContext).ToListAsync();
            var collectionUsers = data.Select(x => new EfModel.CollectionUser()
            {
                CollectionId = _collectionId,
                OrganizationUserId = x.Id,
                ReadOnly = _users.FirstOrDefault(u => u.Id.Equals(x.Id)).ReadOnly,
                HidePasswords = _users.FirstOrDefault(u => u.Id.Equals(x.Id)).HidePasswords,
            });
            return collectionUsers;
        }
    }

    public class CollectionUserUpdateUsersUpdateQuery : IQuery<EfModel.CollectionUser>
    {
        private readonly Guid _collectionId;
        private readonly IEnumerable<SelectionReadOnly> _users;

        public CollectionUserUpdateUsersUpdateQuery(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            _collectionId = collectionId;
            _users = users;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == _collectionId)?.OrganizationId;
            var ids = _users.Select(x => x.Id);
            var updateQuery = from target in dbContext.CollectionUsers
                              where target.CollectionId == _collectionId &&
                                  ids.Contains(target.OrganizationUserId)
                              select target;
            return updateQuery;
        }

        public async Task<IEnumerable<EfModel.CollectionUser>> BuildInMemory(DatabaseContext dbContext)
        {
            var data = await Run(dbContext).ToListAsync();
            var collectionUsers = data.Select(x => new EfModel.CollectionUser
            {
                CollectionId = _collectionId,
                OrganizationUserId = x.OrganizationUserId,
                ReadOnly = _users.FirstOrDefault(u => u.Id.Equals(x.OrganizationUserId)).ReadOnly,
                HidePasswords = _users.FirstOrDefault(u => u.Id.Equals(x.OrganizationUserId)).HidePasswords,
            });
            return collectionUsers;
        }
    }

    public class CollectionUserUpdateUsersDeleteQuery : IQuery<EfModel.CollectionUser>
    {
        private readonly Guid _collectionId;
        private readonly IEnumerable<SelectionReadOnly> _users;

        public CollectionUserUpdateUsersDeleteQuery(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            _collectionId = collectionId;
            _users = users;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == _collectionId)?.OrganizationId;
            var deleteQuery = from cu in dbContext.CollectionUsers
                              where !dbContext.Users.Any(
                                  u => u.Id == cu.OrganizationUserId)
                              select cu;
            return deleteQuery;
        }
    }
}
