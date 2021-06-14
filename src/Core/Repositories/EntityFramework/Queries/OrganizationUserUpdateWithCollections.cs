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
        private readonly OrganizationUser _organizationUser;
        private readonly IEnumerable<SelectionReadOnly> _collections;

        public OrganizationUserUpdateWithCollectionsInsert(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            _organizationUser = organizationUser;
            _collections = collections;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var collectionIds = _collections.Select(c => c.Id).ToArray();
            var t = (from cu in dbContext.CollectionUsers
                    where collectionIds.Contains(cu.CollectionId) &&
                        cu.OrganizationUserId == _organizationUser.Id
                    select cu).AsEnumerable();
            var insertQuery =   (from c in dbContext.Collections
                                where collectionIds.Contains(c.Id) &&
                                    c.OrganizationId == _organizationUser.OrganizationId &&
                                    !t.Any()
                                select c).AsEnumerable();
            return insertQuery.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = x.Id,
                OrganizationUserId = _organizationUser.Id,
                ReadOnly = _collections.FirstOrDefault(c => c.Id == x.Id).ReadOnly,
                HidePasswords = _collections.FirstOrDefault(c => c.Id == x.Id).HidePasswords,
            }).AsQueryable();
        }
    }

    public class OrganizationUserUpdateWithCollectionsUpdate: IQuery<EfModel.CollectionUser>
    {
        private readonly OrganizationUser _organizationUser;
        private readonly IEnumerable<SelectionReadOnly> _collections;

        public OrganizationUserUpdateWithCollectionsUpdate(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            _organizationUser = organizationUser;
            _collections = collections;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var collectionIds = _collections.Select(c => c.Id).ToArray();
            var updateQuery = (from target in dbContext.CollectionUsers
                              where collectionIds.Contains(target.CollectionId) &&
                              target.OrganizationUserId == _organizationUser.Id
                              select new { target }).AsEnumerable();
            updateQuery = updateQuery.Where(cu => 
                cu.target.ReadOnly == _collections.FirstOrDefault(u => u.Id == cu.target.CollectionId).ReadOnly &&
                cu.target.HidePasswords == _collections.FirstOrDefault(u => u.Id == cu.target.CollectionId).HidePasswords);
            return updateQuery.Select(x => new EfModel.CollectionUser(){ 
                CollectionId = x.target.CollectionId,
                OrganizationUserId = _organizationUser.Id,
                ReadOnly = x.target.ReadOnly,
                HidePasswords = x.target.HidePasswords
            }).AsQueryable();
        }
    }

    public class OrganizationUserUpdateWithCollectionsDelete: IQuery<EfModel.CollectionUser>
    {
        private readonly OrganizationUser _organizationUser;
        private readonly IEnumerable<SelectionReadOnly> _collections;

        public OrganizationUserUpdateWithCollectionsDelete(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            _organizationUser = organizationUser;
            _collections = collections;
        }

        public IQueryable<EfModel.CollectionUser> Run(DatabaseContext dbContext)
        {
            var deleteQuery =   from cu in dbContext.CollectionUsers
                                where !_collections.Any(
                                    c => c.Id == cu.CollectionId)
                                select new { cu };
            return deleteQuery.Select(x => x.cu);
        }
    }
}
