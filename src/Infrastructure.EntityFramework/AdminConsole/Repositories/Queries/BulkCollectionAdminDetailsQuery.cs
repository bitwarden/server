using Bit.Core.Enums;
using Bit.Core.Models.Data;

using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;

/// <summary>
/// Query to get collection details, including permissions for the specified user, for many collections at once.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="CollectionAdminDetailsQuery"/> on purpose. Only this query requires a Confirmed
/// membership in an Enabled organization. Adding it as a third mode would change the single-collection and
/// by-organization paths too.
/// </remarks>
public class BulkCollectionAdminDetailsQuery : IQuery<CollectionAdminDetails>
{
    private readonly Guid? _userId;
    private readonly IEnumerable<Guid> _collectionIds;

    public BulkCollectionAdminDetailsQuery(IEnumerable<Guid> collectionIds, Guid? userId)
    {
        _collectionIds = collectionIds;
        _userId = userId;
    }

    public virtual IQueryable<CollectionAdminDetails> Run(DatabaseContext dbContext)
    {
        // Only a Confirmed member of an Enabled org counts as Assigned or Manage.
        var confirmedOrganizationUsers = from ou in dbContext.OrganizationUsers
                                         join o in dbContext.Organizations on ou.OrganizationId equals o.Id
                                         where ou.Status == OrganizationUserStatusType.Confirmed && o.Enabled
                                         select ou;

        var baseCollectionQuery = from c in dbContext.Collections
                                  where _collectionIds.Contains(c.Id)

                                  join ou in confirmedOrganizationUsers
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

        return baseCollectionQuery.Select(x => new CollectionAdminDetails
        {
            Id = x.c.Id,
            OrganizationId = x.c.OrganizationId,
            Name = x.c.Name,
            ExternalId = x.c.ExternalId,
            CreationDate = x.c.CreationDate,
            RevisionDate = x.c.RevisionDate,
            DefaultUserCollectionEmail = x.c.DefaultUserCollectionEmail,
            Type = x.c.Type,
            ReadOnly = (bool?)x.cu.ReadOnly ?? (bool?)x.cg.ReadOnly ?? false,
            HidePasswords = (bool?)x.cu.HidePasswords ?? (bool?)x.cg.HidePasswords ?? false,
            Manage = (bool?)x.cu.Manage ?? (bool?)x.cg.Manage ?? false,
            Assigned = x.cu != null || x.cg != null,
            Unmanaged = !activeUserManageRights.Contains(x.c.Id) && !activeGroupManageRights.Contains(x.c.Id),
            // A disabled rule gates nothing, so the association alone is not enough.
            HasEnabledAccessRule = dbContext.AccessRules.Any(ar => ar.Id == x.c.AccessRuleId && ar.Enabled),
        });
    }
}
