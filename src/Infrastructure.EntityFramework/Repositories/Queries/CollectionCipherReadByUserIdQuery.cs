using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class CollectionCipherReadByUserIdQuery : IQuery<CollectionCipher>
{
    private readonly Guid _userId;

    public CollectionCipherReadByUserIdQuery(Guid userId)
    {
        _userId = userId;
    }

    public virtual IQueryable<CollectionCipher> Run(DatabaseContext dbContext)
    {
        var query = from cc in dbContext.CollectionCiphers

                    join c in dbContext.Collections
                        on cc.CollectionId equals c.Id

                    join ou in dbContext.OrganizationUsers
                        on new { c.OrganizationId, UserId = (Guid?)_userId } equals
                           new { ou.OrganizationId, ou.UserId }

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

                    where ou.Status == OrganizationUserStatusType.Confirmed &&
                        ((cu == null ? (Guid?)null : cu.CollectionId) != null || (cg == null ? (Guid?)null : cg.CollectionId) != null)
                    select cc;
        return query;
    }
}
