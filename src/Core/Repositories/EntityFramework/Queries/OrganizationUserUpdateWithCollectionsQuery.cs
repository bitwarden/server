using Bit.Core.Models.Data;
using Bit.Core.Models.EntityFramework;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;
using Table = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserUpdateWithCollectionsQuery
    {
        public OrganizationUserUpdateWithCollectionsInsertQuery Insert { get; set; }
        public OrganizationUserUpdateWithCollectionsUpdateQuery Update { get; set; }
        public OrganizationUserUpdateWithCollectionsDeleteQuery Delete { get; set; }

        public OrganizationUserUpdateWithCollectionsQuery(Table.OrganizationUser organizationUser,
                IEnumerable<SelectionReadOnly> collections)
        {
            Insert  = new OrganizationUserUpdateWithCollectionsInsertQuery(organizationUser, collections);
            Update = new OrganizationUserUpdateWithCollectionsUpdateQuery(organizationUser, collections);
            Delete = new OrganizationUserUpdateWithCollectionsDeleteQuery(organizationUser, collections);
        }
    }

    public class OrganizationUserUpdateWithCollectionsInsertQuery : IQuery<CollectionUser>
    {
        private readonly Table.OrganizationUser _organizationUser;
        private readonly IEnumerable<SelectionReadOnly> _collections;

        public OrganizationUserUpdateWithCollectionsInsertQuery(Table.OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            _organizationUser = organizationUser;
            _collections = collections;
        }

        public IQueryable<CollectionUser> Run(DatabaseContext dbContext)
        {
            var collectionIds = _collections.Select(c => c.Id).ToArray();
            var t = (from cu in dbContext.CollectionUsers
                where collectionIds.Contains(cu.CollectionId) &&
                    cu.OrganizationUserId == _organizationUser.Id
                select cu).AsEnumerable();
            var insertQuery = (from c in dbContext.Collections
                where collectionIds.Contains(c.Id) &&
                    c.OrganizationId == _organizationUser.OrganizationId &&
                    !t.Any()
                select c).AsEnumerable();
            return insertQuery.Select(x => new CollectionUser(){ 
                CollectionId = x.Id,
                OrganizationUserId = _organizationUser.Id,
                ReadOnly = _collections.FirstOrDefault(c => c.Id == x.Id).ReadOnly,
                HidePasswords = _collections.FirstOrDefault(c => c.Id == x.Id).HidePasswords,
            }).AsQueryable();
        }
    }

    public class OrganizationUserUpdateWithCollectionsUpdateQuery: IQuery<CollectionUser>
    {
        private readonly Table.OrganizationUser _organizationUser;
        private readonly IEnumerable<SelectionReadOnly> _collections;

        public OrganizationUserUpdateWithCollectionsUpdateQuery(Table.OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            _organizationUser = organizationUser;
            _collections = collections;
        }

        public IQueryable<CollectionUser> Run(DatabaseContext dbContext)
        {
            var collectionIds = _collections.Select(c => c.Id).ToArray();
            var updateQuery = (from target in dbContext.CollectionUsers
                where collectionIds.Contains(target.CollectionId) &&
                target.OrganizationUserId == _organizationUser.Id
                select new { target }).AsEnumerable();
            updateQuery = updateQuery.Where(cu => 
                cu.target.ReadOnly == _collections.FirstOrDefault(u => u.Id == cu.target.CollectionId).ReadOnly &&
                cu.target.HidePasswords == _collections.FirstOrDefault(u => u.Id == cu.target.CollectionId).HidePasswords);
            return updateQuery.Select(x => new CollectionUser(){ 
                CollectionId = x.target.CollectionId,
                OrganizationUserId = _organizationUser.Id,
                ReadOnly = x.target.ReadOnly,
                HidePasswords = x.target.HidePasswords
            }).AsQueryable();
        }
    }

    public class OrganizationUserUpdateWithCollectionsDeleteQuery: IQuery<CollectionUser>
    {
        private readonly Table.OrganizationUser _organizationUser;
        private readonly IEnumerable<SelectionReadOnly> _collections;

        public OrganizationUserUpdateWithCollectionsDeleteQuery(Table.OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections)
        {
            _organizationUser = organizationUser;
            _collections = collections;
        }

        public IQueryable<CollectionUser> Run(DatabaseContext dbContext)
        {
            var deleteQuery = from cu in dbContext.CollectionUsers
                where !_collections.Any(
                    c => c.Id == cu.CollectionId)
                select cu;
            return deleteQuery;
        }
    }
}
