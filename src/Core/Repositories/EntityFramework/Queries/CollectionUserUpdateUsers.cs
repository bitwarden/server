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
        public CollectionUserUpdateUsersInsert Insert { get; set; }
        public CollectionUserUpdateUsersUpdate Update { get; set; }
        public CollectionUserUpdateUsersDelete Delete { get; set; }

        public CollectionUserUpdateUsers(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            Insert  = new CollectionUserUpdateUsersInsert(collectionId, users);
            Update = new CollectionUserUpdateUsersUpdate(collectionId, users);
            Delete = new CollectionUserUpdateUsersDelete(collectionId, users);
        }
    }

    public class CollectionUserUpdateUsersInsert : IQuery<EfModel.OrganizationUser>
    {
        private Guid CollectionId { get; set; }
        private IEnumerable<SelectionReadOnly> Users { get; set; }

        public CollectionUserUpdateUsersInsert(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            CollectionId = collectionId;
            Users = users;
        }

        public IQueryable<EfModel.OrganizationUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == CollectionId)?.OrganizationId;
            var organizationUserIds = Users.Select(u => u.Id);
            var insertQuery =   from ou in dbContext.OrganizationUsers
                                where 
                                    organizationUserIds.Contains(ou.Id) &&
                                    ou.OrganizationId == orgId &&
                                    !dbContext.CollectionUsers.Any(
                                        x => x.CollectionId != CollectionId && x.OrganizationUserId == ou.Id)
                                select ou;
            return insertQuery;
        }

        public async Task<IEnumerable<EfModel.CollectionUser>> BuildInMemory(DatabaseContext dbContext)
        {
            var data = await Run(dbContext).ToListAsync();
            var collectionUsers = data.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = CollectionId,
                OrganizationUserId = x.Id,
                ReadOnly = Users.FirstOrDefault(u => u.Id.Equals(x.Id)).ReadOnly,
                HidePasswords = Users.FirstOrDefault(u => u.Id.Equals(x.Id)).HidePasswords
            });
            return collectionUsers;
        }
    }

    public class CollectionUserUpdateUsersUpdate: IQuery<EfModel.CollectionUser>
    {
        private Guid CollectionId { get; set; }
        private IEnumerable<SelectionReadOnly> Users { get; set; }

        public CollectionUserUpdateUsersUpdate(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            CollectionId = collectionId;
            Users = users;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == CollectionId)?.OrganizationId;
            var ids = Users.Select(x => x.Id);
            var updateQuery =   from target in dbContext.CollectionUsers
                                where target.CollectionId == CollectionId &&
                                    ids.Contains(target.OrganizationUserId)
                                select target;
            return updateQuery;
        }

        public async Task<IEnumerable<EfModel.CollectionUser>> BuildInMemory(DatabaseContext dbContext)
        {
            var data = await Run(dbContext).ToListAsync();
            var collectionUsers = data.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = CollectionId,
                OrganizationUserId = x.OrganizationUserId,
                ReadOnly = Users.FirstOrDefault(u => u.Id.Equals(x.OrganizationUserId)).ReadOnly,
                HidePasswords = Users.FirstOrDefault(u => u.Id.Equals(x.OrganizationUserId)).HidePasswords
            });
            return collectionUsers;
        }
    }

    public class CollectionUserUpdateUsersDelete: IQuery<EfModel.CollectionUser>
    {
        private Guid CollectionId { get; set; }
        private IEnumerable<SelectionReadOnly> Users { get; set; }

        public CollectionUserUpdateUsersDelete(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            CollectionId = collectionId;
            Users = users;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == CollectionId)?.OrganizationId;
            var deleteQuery =   from cu in dbContext.CollectionUsers
                                where !dbContext.Users.Any(
                                    u => u.Id == cu.OrganizationUserId)
                                select new { cu };
            return deleteQuery.Select(x => x.cu);
        }
    }
}
