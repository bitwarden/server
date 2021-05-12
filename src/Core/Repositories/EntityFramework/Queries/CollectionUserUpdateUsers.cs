using System.Collections.Generic;
using System.Linq;
using EfModel = Bit.Core.Models.EntityFramework;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;
using System;

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

    public class CollectionUserUpdateUsersInsert : IQuery<EfModel.CollectionUser>
    {
        private Guid CollectionId { get; set; }
        private IEnumerable<SelectionReadOnly> Users { get; set; }

        public CollectionUserUpdateUsersInsert(Guid collectionId, IEnumerable<SelectionReadOnly> users)
        {
            CollectionId = collectionId;
            Users = users;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var orgId = dbContext.Collections.FirstOrDefault(c => c.Id == CollectionId)?.OrganizationId;
            var insertQuery =   from source in Users 
                                join ou in dbContext.OrganizationUsers
                                    on source.Id equals ou.Id
                                where 
                                    ou.OrganizationId == orgId &&
                                    !dbContext.CollectionUsers.Any(
                                        x => x.CollectionId != CollectionId && x.OrganizationUserId == ou.Id)
                                select new { source, ou };
            return insertQuery.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = CollectionId,
                OrganizationUserId = x.source.Id,
                ReadOnly = x.source.ReadOnly,
                HidePasswords = x.source.HidePasswords
            }).AsQueryable();
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
            var userIds = Users.Select(u => u.Id).ToArray();
            var updateQuery =   (from target in dbContext
                                    .CollectionUsers
                                    .Where(cu => userIds.Contains(cu.OrganizationUserId) && cu.CollectionId == CollectionId)
                                select new { target }).AsEnumerable();
            updateQuery = updateQuery.Where(cu => 
                cu.target.ReadOnly == Users.FirstOrDefault(u => u.Id == cu.target.OrganizationUserId).ReadOnly &&
                cu.target.HidePasswords == Users.FirstOrDefault(u => u.Id == cu.target.OrganizationUserId).HidePasswords);
            return updateQuery.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = CollectionId,
                OrganizationUserId = x.target.OrganizationUserId,
                ReadOnly = x.target.ReadOnly,
                HidePasswords = x.target.HidePasswords
            }).AsQueryable();
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
