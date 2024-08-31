using Bit.Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

/// <summary>
/// Query to get collection details, including permissions for the specified user if provided.
/// </summary>
public class CollectionAdminDetailsQuery : IQuery<CollectionAdminDetails>
{
    private readonly Guid? _userId;
    private readonly Guid? _organizationId;
    private readonly Guid? _collectionId;

    private CollectionAdminDetailsQuery(Guid? userId, Guid? organizationId, Guid? collectionId)
    {
        _userId = userId;
        _organizationId = organizationId;
        _collectionId = collectionId;
    }

    public virtual IQueryable<CollectionAdminDetails> Run(DatabaseContext dbContext)
    {
        var baseCollectionQuery = from c in dbContext.Collections
                                  join ou in dbContext.OrganizationUsers
                                      on new { c.OrganizationId, UserId = _userId } equals
                                      new { ou.OrganizationId, ou.UserId } into ou_g
                                  from ou in ou_g.DefaultIfEmpty()

                                  join cu in dbContext.CollectionUsers
                                      on new { CollectionId = c.Id, OrganizationUserId = ou.Id } equals
                                      new { cu.CollectionId, cu.OrganizationUserId } into cu_g
                                  from cu in cu_g.DefaultIfEmpty()

                                  join gu in dbContext.GroupUsers
                                      on new { CollectionId = (Guid?)cu.CollectionId, OrganizationUserId = ou.Id } equals
                                      new { CollectionId = (Guid?)null, gu.OrganizationUserId } into gu_g
                                  from gu in gu_g.DefaultIfEmpty()

                                  join g in dbContext.Groups
                                      on gu.GroupId equals g.Id into g_g
                                  from g in g_g.DefaultIfEmpty()

                                  join cg in dbContext.CollectionGroups
                                      on new { CollectionId = c.Id, gu.GroupId } equals
                                      new { cg.CollectionId, cg.GroupId } into cg_g
                                  from cg in cg_g.DefaultIfEmpty()
                                  select new { c, cu, cg };

        // Subqueries to determine if a collection is managed by a user or group.
        var activeUserManageRights = from cu in dbContext.CollectionUsers
                                     join ou in dbContext.OrganizationUsers
                                         on cu.OrganizationUserId equals ou.Id
                                     where cu.Manage
                                     select cu.CollectionId;

        var activeGroupManageRights = from cg in dbContext.CollectionGroups
                                      where cg.Manage
                                      select cg.CollectionId;

        if (_organizationId.HasValue)
        {
            baseCollectionQuery = baseCollectionQuery.Where(x => x.c.OrganizationId == _organizationId);
        }
        else if (_collectionId.HasValue)
        {
            baseCollectionQuery = baseCollectionQuery.Where(x => x.c.Id == _collectionId);
        }
        else
        {
            throw new InvalidOperationException("OrganizationId or CollectionId must be specified.");
        }

        return baseCollectionQuery.Select(x => new CollectionAdminDetails
        {
            Id = x.c.Id,
            OrganizationId = x.c.OrganizationId,
            Name = x.c.Name,
            ExternalId = x.c.ExternalId,
            CreationDate = x.c.CreationDate,
            RevisionDate = x.c.RevisionDate,
            ReadOnly = (bool?)x.cu.ReadOnly ?? (bool?)x.cg.ReadOnly ?? false,
            HidePasswords = (bool?)x.cu.HidePasswords ?? (bool?)x.cg.HidePasswords ?? false,
            Manage = (bool?)x.cu.Manage ?? (bool?)x.cg.Manage ?? false,
            Assigned = x.cu != null || x.cg != null,
            Unmanaged = !activeUserManageRights.Contains(x.c.Id) && !activeGroupManageRights.Contains(x.c.Id),
        });
    }

    public static CollectionAdminDetailsQuery ByCollectionId(Guid collectionId, Guid? userId)
    {
        return new CollectionAdminDetailsQuery(userId, null, collectionId);
    }

    public static CollectionAdminDetailsQuery ByOrganizationId(Guid organizationId, Guid? userId)
    {
        return new CollectionAdminDetailsQuery(userId, organizationId, null);
    }

}
