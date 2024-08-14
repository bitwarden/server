using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

/// <summary>
/// Returns all Collections that a user is assigned to in an organization, either directly or via a group.
/// </summary>
public class CollectionsReadByOrganizationIdUserIdQuery : IQuery<Collection>
{
    private readonly Guid? _organizationId;
    private readonly Guid _userId;

    public CollectionsReadByOrganizationIdUserIdQuery(Guid? organizationId, Guid userId)
    {
        _organizationId = organizationId;
        _userId = userId;
    }

    public virtual IQueryable<Collection> Run(DatabaseContext dbContext)
    {
        var query = from c in dbContext.Collections
                    join o in dbContext.Organizations on c.OrganizationId equals o.Id
                    join ou in dbContext.OrganizationUsers
                        on new { OrganizationId = o.Id, UserId = (Guid?)_userId } equals
                        new { ou.OrganizationId, ou.UserId }
                    join cu in dbContext.CollectionUsers
                        on new { CollectionId = c.Id, OrganizationUserId = ou.Id } equals
                        new { cu.CollectionId, cu.OrganizationUserId } into cu_g
                    from cu in cu_g.DefaultIfEmpty()
                    join gu in dbContext.GroupUsers
                        on new { CollectionId = (Guid?)cu.CollectionId, OrganizationUserId = ou.Id } equals
                        new { CollectionId = (Guid?)null, gu.OrganizationUserId } into gu_g
                    from gu in gu_g.DefaultIfEmpty()
                    join g in dbContext.Groups on gu.GroupId equals g.Id into g_g
                    from g in g_g.DefaultIfEmpty()
                    join cg in dbContext.CollectionGroups
                        on new { CollectionId = c.Id, gu.GroupId } equals
                        new { cg.CollectionId, cg.GroupId } into cg_g
                    from cg in cg_g.DefaultIfEmpty()
                    where o.Id == _organizationId && o.Enabled && ou.Status == OrganizationUserStatusType.Confirmed
                          && (!cu.ReadOnly || !cg.ReadOnly)
                    select c;

        return query;
    }
}
