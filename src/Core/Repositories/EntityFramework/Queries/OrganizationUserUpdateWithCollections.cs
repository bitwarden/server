using System.Collections.Generic;
using System.Linq;
using EfModel = Bit.Core.Models.EntityFramework;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;
using System;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserUpdateWithCollections
    {
        public OrganizationUserUpdateWithCollectionsInsert Insert { get; set; }
        public OrganizationUserUpdateWithCollectionsUpdate Update { get; set; }
        public OrganizationUserUpdateWithCollectionsDelete Delete { get; set; }

        public OrganizationUserUpdateWithCollections(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            Insert  = new OrganizationUserUpdateWithCollectionsInsert(organizationUser, collections);
            Update = new OrganizationUserUpdateWithCollectionsUpdate(organizationUser, collections);
            Delete = new OrganizationUserUpdateWithCollectionsDelete(organizationUser, collections);
        }
    }

    public class OrganizationUserUpdateWithCollectionsInsert : IQuery<EfModel.CollectionUser>
    {
        private OrganizationUser OrganizationUser { get; set; }
        private IEnumerable<SelectionReadOnly> Collections { get; set; }

        public OrganizationUserUpdateWithCollectionsInsert(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            OrganizationUser = organizationUser;
            Collections = collections;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var collectionIds = Collections.Select(c => c.Id).ToArray();
            var t = (from cu in dbContext.CollectionUsers
                    where collectionIds.Contains(cu.CollectionId) &&
                        cu.OrganizationUserId == OrganizationUser.Id
                    select cu).AsEnumerable();
            var insertQuery =   (from c in dbContext.Collections
                                where collectionIds.Contains(c.Id) &&
                                    c.OrganizationId == OrganizationUser.OrganizationId &&
                                    !t.Any()
                                select c).AsEnumerable();
            return insertQuery.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = x.Id,
                OrganizationUserId = OrganizationUser.Id,
                ReadOnly = Collections.FirstOrDefault(c => c.Id == x.Id).ReadOnly,
                HidePasswords = Collections.FirstOrDefault(c => c.Id == x.Id).HidePasswords,
            }).AsQueryable();
        }
    }

    public class OrganizationUserUpdateWithCollectionsUpdate: IQuery<EfModel.CollectionUser>
    {
        private OrganizationUser OrganizationUser { get; set; }
        private IEnumerable<SelectionReadOnly> Collections { get; set; }

        public OrganizationUserUpdateWithCollectionsUpdate(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            OrganizationUser = organizationUser;
            Collections = collections;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var collectionIds = Collections.Select(c => c.Id).ToArray();
            var updateQuery = (from target in dbContext.CollectionUsers
                              where collectionIds.Contains(target.CollectionId) &&
                              target.OrganizationUserId == OrganizationUser.Id
                              select new { target }).AsEnumerable();
            updateQuery = updateQuery.Where(cu => 
                cu.target.ReadOnly == Collections.FirstOrDefault(u => u.Id == cu.target.CollectionId).ReadOnly &&
                cu.target.HidePasswords == Collections.FirstOrDefault(u => u.Id == cu.target.CollectionId).HidePasswords);
            return updateQuery.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = x.target.CollectionId,
                OrganizationUserId = OrganizationUser.Id,
                ReadOnly = x.target.ReadOnly,
                HidePasswords = x.target.HidePasswords
            }).AsQueryable();
        }
    }

    public class OrganizationUserUpdateWithCollectionsDelete: IQuery<EfModel.CollectionUser>
    {
        private OrganizationUser OrganizationUser  { get; set; }
        private IEnumerable<SelectionReadOnly> Collections { get; set; }

        public OrganizationUserUpdateWithCollectionsDelete(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            OrganizationUser = organizationUser;
            Collections = collections;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var deleteQuery =   from cu in dbContext.CollectionUsers
                                where !Collections.Any(
                                    c => c.Id == cu.CollectionId)
                                select new { cu };
            return deleteQuery.Select(x => x.cu);
        }
    }
}
