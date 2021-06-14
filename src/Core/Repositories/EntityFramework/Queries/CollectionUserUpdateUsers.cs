using System.Collections.Generic;
using System.Linq;
using EfModel = Bit.Core.Models.EntityFramework;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CollectionUserUpdateUsers
    {
        public readonly CollectionUserUpdateUsersInsert Insert;
        public readonly CollectionUserUpdateUsersUpdate Update;
        public readonly CollectionUserUpdateUsersDelete Delete;

        public CollectionUserUpdateUsers(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            Insert  = new CollectionUserUpdateUsersInsert(collectionId, users);
            Update = new CollectionUserUpdateUsersUpdate(collectionId, users);
            Delete = new CollectionUserUpdateUsersDelete(collectionId, users);
        }
    }

    public class CollectionUserUpdateUsersInsert : IQuery<EfModel.OrganizationUser>
    {
        private readonly Guid _collectionId;
        private readonly IEnumerable<SelectionReadOnly> _users;

        public CollectionUserUpdateUsersInsert(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            _collectionId = collectionId;
            _users = users;
        }

        public IQueryable<EfModel.OrganizationUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == _collectionId)?.OrganizationId;
            var organizationUserIds = _users.Select(u => u.Id);
            var insertQuery =   from ou in dbContext.OrganizationUsers
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
            var collectionUsers = data.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = _collectionId,
                OrganizationUserId = x.Id,
                ReadOnly = _users.FirstOrDefault(u => u.Id.Equals(x.Id)).ReadOnly,
                HidePasswords = _users.FirstOrDefault(u => u.Id.Equals(x.Id)).HidePasswords
            });
            return collectionUsers;
        }
    }

    public class CollectionUserUpdateUsersUpdate: IQuery<EfModel.CollectionUser>
    {
        private readonly Guid _collectionId;
        private readonly IEnumerable<SelectionReadOnly> _users;

        public CollectionUserUpdateUsersUpdate(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            _collectionId = collectionId;
            _users = users;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == _collectionId)?.OrganizationId;
            var ids = _users.Select(x => x.Id);
            var updateQuery =   from target in dbContext.CollectionUsers
                                where target.CollectionId == _collectionId &&
                                    ids.Contains(target.OrganizationUserId)
                                select target;
            return updateQuery;
        }

        public async Task<IEnumerable<EfModel.CollectionUser>> BuildInMemory(DatabaseContext dbContext)
        {
            var data = await Run(dbContext).ToListAsync();
            var collectionUsers = data.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = _collectionId,
                OrganizationUserId = x.OrganizationUserId,
                ReadOnly = _users.FirstOrDefault(u => u.Id.Equals(x.OrganizationUserId)).ReadOnly,
                HidePasswords = _users.FirstOrDefault(u => u.Id.Equals(x.OrganizationUserId)).HidePasswords
            });
            return collectionUsers;
        }
    }

    public class CollectionUserUpdateUsersDelete: IQuery<EfModel.CollectionUser>
    {
        private readonly Guid _collectionId;
        private readonly IEnumerable<SelectionReadOnly> _users;

        public CollectionUserUpdateUsersDelete(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            _collectionId = collectionId;
            _users = users;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == _collectionId)?.OrganizationId;
            var deleteQuery =   from cu in dbContext.CollectionUsers
                                where !dbContext.Users.Any(
                                    u => u.Id == cu.OrganizationUserId)
                                select new { cu };
            return deleteQuery.Select(x => x.cu);
        }
    }
}
