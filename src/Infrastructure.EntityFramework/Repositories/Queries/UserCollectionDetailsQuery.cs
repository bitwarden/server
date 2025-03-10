using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class UserCollectionDetailsQuery : IQuery<CollectionDetails>
{
    private readonly Guid? _userId;

    public UserCollectionDetailsQuery(Guid? userId)
    {
        _userId = userId;
    }

    public virtual IQueryable<CollectionDetails> Run(DatabaseContext dbContext)
    {
        var query = from c in dbContext.Collections

                    join ou in dbContext.OrganizationUsers
                        on c.OrganizationId equals ou.OrganizationId

                    join o in dbContext.Organizations
                        on c.OrganizationId equals o.Id

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

                    where ou.UserId == _userId &&
                        ou.Status == OrganizationUserStatusType.Confirmed &&
                        o.Enabled &&
                        ((cu == null ? (Guid?)null : cu.CollectionId) != null || (cg == null ? (Guid?)null : cg.CollectionId) != null)
                    select new { c, ou, o, cu, gu, g, cg };

        return query.Select(x => new CollectionDetails
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
        });
    }
}
