using Bit.Core.Entities;
using Bit.Core.Models.Data;
using CollectionUser = Bit.Infrastructure.EntityFramework.Models.CollectionUser;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserUpdateWithCollectionsQuery
{
    public OrganizationUserUpdateWithCollectionsInsertQuery Insert { get; set; }
    public OrganizationUserUpdateWithCollectionsUpdateQuery Update { get; set; }
    public OrganizationUserUpdateWithCollectionsDeleteQuery Delete { get; set; }

    public OrganizationUserUpdateWithCollectionsQuery(OrganizationUser organizationUser,
            IEnumerable<CollectionAccessSelection> collections)
    {
        Insert = new OrganizationUserUpdateWithCollectionsInsertQuery(organizationUser, collections);
        Update = new OrganizationUserUpdateWithCollectionsUpdateQuery(organizationUser, collections);
        Delete = new OrganizationUserUpdateWithCollectionsDeleteQuery(organizationUser, collections);
    }
}

public class OrganizationUserUpdateWithCollectionsInsertQuery : IQuery<CollectionUser>
{
    private readonly OrganizationUser _organizationUser;
    private readonly IEnumerable<CollectionAccessSelection> _collections;

    public OrganizationUserUpdateWithCollectionsInsertQuery(OrganizationUser organizationUser, IEnumerable<CollectionAccessSelection> collections)
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
        return insertQuery.Select(x => new CollectionUser
        {
            CollectionId = x.Id,
            OrganizationUserId = _organizationUser.Id,
            ReadOnly = _collections.FirstOrDefault(c => c.Id == x.Id).ReadOnly,
            HidePasswords = _collections.FirstOrDefault(c => c.Id == x.Id).HidePasswords,
        }).AsQueryable();
    }
}

public class OrganizationUserUpdateWithCollectionsUpdateQuery : IQuery<CollectionUser>
{
    private readonly OrganizationUser _organizationUser;
    private readonly IEnumerable<CollectionAccessSelection> _collections;

    public OrganizationUserUpdateWithCollectionsUpdateQuery(OrganizationUser organizationUser, IEnumerable<CollectionAccessSelection> collections)
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
        return updateQuery.Select(x => new CollectionUser
        {
            CollectionId = x.target.CollectionId,
            OrganizationUserId = _organizationUser.Id,
            ReadOnly = x.target.ReadOnly,
            HidePasswords = x.target.HidePasswords,
        }).AsQueryable();
    }
}

public class OrganizationUserUpdateWithCollectionsDeleteQuery : IQuery<CollectionUser>
{
    private readonly OrganizationUser _organizationUser;
    private readonly IEnumerable<CollectionAccessSelection> _collections;

    public OrganizationUserUpdateWithCollectionsDeleteQuery(OrganizationUser organizationUser, IEnumerable<CollectionAccessSelection> collections)
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
