using Bit.Core.Enums;
using User = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class UserBumpAccountRevisionDateByCipherIdQuery : IQuery<User>
{
    private readonly Guid _cipherId;
    private readonly Guid _organizationId;

    public UserBumpAccountRevisionDateByCipherIdQuery(Guid cipherId, Guid organizationId)
    {
        _cipherId = cipherId;
        _organizationId = organizationId;
    }

    public IQueryable<User> Run(DatabaseContext dbContext)
    {
        var query = from u in dbContext.Users

                    join ou in dbContext.OrganizationUsers
                        on u.Id equals ou.UserId

                    join collectionCipher in dbContext.CollectionCiphers
                        on _cipherId equals collectionCipher.CipherId into cc_g
                    from cc in cc_g.DefaultIfEmpty()

                    join collectionUser in dbContext.CollectionUsers
                        on new { OrganizationUserId = ou.Id, cc.CollectionId } equals
                           new { collectionUser.OrganizationUserId, collectionUser.CollectionId } into cu_g
                    from cu in cu_g.DefaultIfEmpty()

                    join groupUser in dbContext.GroupUsers
                        on new { CollectionId = (Guid?)cu.CollectionId, OrganizationUserId = ou.Id } equals
                           new { CollectionId = (Guid?)null, groupUser.OrganizationUserId } into gu_g
                    from gu in gu_g.DefaultIfEmpty()

                    join grp in dbContext.Groups
                        on gu.GroupId equals grp.Id into g_g
                    from g in g_g.DefaultIfEmpty()

                    join collectionGroup in dbContext.CollectionGroups
                        on new { gu.GroupId, cc.CollectionId } equals
                           new { collectionGroup.GroupId, collectionGroup.CollectionId } into cg_g
                    from cg in cg_g.DefaultIfEmpty()

                    where ou.OrganizationId == _organizationId &&
                            ou.Status == OrganizationUserStatusType.Confirmed &&
                            ((cu == null ? (Guid?)null : cu.CollectionId) != null ||
                            (cg == null ? (Guid?)null : cg.CollectionId) != null)
                    select u;
        return query;
    }
}
